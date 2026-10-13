using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Game.Core
{
    /// <summary>로비 진입 전 로그인 확인.
    /// 저장된 로그인 정보가 없으면(최초 진입) 로그인창에서 방식(구글·애플·게스트)을 고르게 하고, 있으면 그 방식으로 바로 로그인한다.
    /// 외부 계정 로그인을 취소하거나 실패하면 로그인창으로 돌아가고, 로그인·플레이어 상태 조회가 실패하면 [다시 시도]를 기다렸다가 다시 시도한다.
    /// 로그인 정보는 성공했을 때 저장한다</summary>
    public sealed class LoginFlow
    {
        private readonly IAuthApi _auth;
        private readonly IPlayerApi _player;
        private readonly PlayerProfile _profile;
        private readonly IAuthCredentialStore _credentials;
        private readonly ILoginPrompt _prompt;
        private readonly IReadOnlyList<ISocialSignIn> _social;

        public LoginFlow(IAuthApi auth, IPlayerApi player, PlayerProfile profile, IAuthCredentialStore credentials, ILoginPrompt prompt,
            IReadOnlyList<ISocialSignIn> social)
        {
            _auth = auth;
            _player = player;
            _profile = profile;
            _credentials = credentials;
            _prompt = prompt;
            _social = social ?? Array.Empty<ISocialSignIn>();
        }

        /// <summary>로그인하고 플레이어 상태를 프로필에 반영할 때까지 기다린다. 끝나면 로그인 화면은 닫혀 있다.
        /// 로그인창을 띄워 사용자가 방식을 골랐으면 true, 저장된 로그인 정보로 바로 로그인했으면 false를 돌려준다</summary>
        public async UniTask<bool> RunAsync()
        {
            bool prompted = false;
            // 저장된 방식을 지금 쓸 수 없으면(예: 외부 계정 SDK가 빠진 빌드) 로그인창부터 다시 띄운다
            LoginMethod? method = _credentials.HasCredential && IsAvailable(_credentials.Method) ? _credentials.Method : null;
            string accountId = method != null ? _credentials.AccountId : null;

            while (true)
            {
                if (method == null)
                {
                    method = await _prompt.WaitForSignInAsync(AvailableMethods());
                    accountId = null;
                    prompted = true;
                }

                // TODO(sdk): 외부 계정은 실행할 때마다 조용히 다시 로그인해 새 토큰을 받는다 (지금은 저장한 ID를 그대로 쓴다)
                if (method != LoginMethod.Guest && accountId == null)
                {
                    accountId = await SocialSignInAsync(method.Value);
                    if (accountId == null)
                    {
                        method = null; // 취소·실패: 로그인창으로 돌아간다
                        continue;
                    }
                }

                if (await TrySignInAsync())
                {
                    break;
                }

                await _prompt.WaitForRetryAsync();
            }

            if (!_credentials.HasCredential || _credentials.Method != method.Value)
            {
                _credentials.Save(method.Value, accountId ?? Guid.NewGuid().ToString("N"));
            }

            _prompt.Hide();
            return prompted;
        }

        private List<LoginMethod> AvailableMethods()
        {
            var methods = new List<LoginMethod> { LoginMethod.Guest };
            foreach (var social in _social)
            {
                if (social.IsAvailable && !methods.Contains(social.Method))
                {
                    methods.Add(social.Method);
                }
            }

            return methods;
        }

        private bool IsAvailable(LoginMethod method) => method == LoginMethod.Guest || Find(method) != null;

        private ISocialSignIn Find(LoginMethod method)
        {
            foreach (var social in _social)
            {
                if (social.Method == method && social.IsAvailable)
                {
                    return social;
                }
            }

            return null;
        }

        private async UniTask<string> SocialSignInAsync(LoginMethod method)
        {
            var social = Find(method);
            if (social == null)
            {
                return null;
            }

            try
            {
                return await social.SignInAsync();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return null;
            }
        }

        private async UniTask<bool> TrySignInAsync()
        {
            _prompt.ShowSigningIn();
            try
            {
                // TODO(server): 로그인 방식과 계정 ID(게스트 자격 증명·외부 계정 토큰)를 넘기는 로그인으로 바꾼다 (#180, server#1)
                await _auth.Login();
                var snapshot = await _player.GetMe();
                _profile.Apply(snapshot);
                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return false;
            }
        }
    }
}
