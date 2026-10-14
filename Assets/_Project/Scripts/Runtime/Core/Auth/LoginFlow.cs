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
            // 게스트는 저장한 게스트 ID로 다시 로그인한다. 외부 계정은 저장한 값이 오래된 토큰이라 쓰지 않고,
            // 실행할 때마다 외부 계정으로 다시 로그인해 새 토큰을 받는다
            string accountId = method == LoginMethod.Guest ? _credentials.AccountId : null;
            LoginResponse response;
            // 저장된 방식으로 바로 들어가는 동안은 계정 선택 없이 로그인을 시도하고, 로그인창에서 사용자가 고른 뒤에는 선택창을 보여 준다
            bool silent = method != null;

            while (true)
            {
                if (method == null)
                {
                    method = await _prompt.WaitForSignInAsync(AvailableMethods());
                    accountId = null;
                    silent = false;
                    prompted = true;
                }

                if (method != LoginMethod.Guest && accountId == null)
                {
                    accountId = await SocialSignInAsync(method.Value, silent);
                    if (accountId == null)
                    {
                        method = null; // 취소·실패: 로그인창으로 돌아간다
                        continue;
                    }
                }

                response = await TrySignInAsync(method.Value, accountId);
                if (response != null)
                {
                    break;
                }

                await _prompt.WaitForRetryAsync();
            }

            // 서버가 게스트 ID를 새로 정해 주면(가입하거나, 저장한 ID가 거절돼 다시 가입한 경우) 그 ID로 바꿔 저장한다
            // 외부 계정의 ID 토큰은 저장하지 않는다 (AuthCredentials.SocialAccountId)
            string saveId = AuthCredentials.IdToSave(method.Value, response.accountId ?? accountId ?? Guid.NewGuid().ToString("N"));
            if (!_credentials.HasCredential || _credentials.Method != method.Value || _credentials.AccountId != saveId)
            {
                _credentials.Save(method.Value, saveId);
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

        private async UniTask<string> SocialSignInAsync(LoginMethod method, bool silent)
        {
            var social = Find(method);
            if (social == null)
            {
                return null;
            }

            try
            {
                return silent ? await social.SignInSilentAsync() : await social.SignInAsync();
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

        /// <summary>로그인하고 플레이어 상태를 프로필에 반영한다. 실패하면 로그만 남기고 null을 돌려준다</summary>
        private async UniTask<LoginResponse> TrySignInAsync(LoginMethod method, string accountId)
        {
            _prompt.ShowSigningIn();
            try
            {
                var response = await _auth.Login(method, accountId);
                var snapshot = await _player.GetMe();
                _profile.Apply(snapshot);
                return response;
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
    }
}
