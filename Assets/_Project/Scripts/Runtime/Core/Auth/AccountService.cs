using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Game.Core
{
    public enum LinkResult
    {
        Linked,
        /// <summary>이미 외부 계정에 연결된 계정이다</summary>
        AlreadyLinked,
        /// <summary>이 기기·빌드에서 쓸 수 있는 외부 계정 로그인이 없다</summary>
        Unavailable,
        /// <summary>사용자가 취소하거나 외부 로그인이 실패했다</summary>
        Cancelled,
    }

    /// <summary>로그아웃·계정 삭제·계정 연동. 게스트 계정은 기기에만 있으므로 로그아웃하면 데이터도 함께 지운다.
    /// 로그아웃과 삭제는 저장된 로그인 정보를 지우고 Loading 씬으로 돌아가 로그인창부터 다시 시작한다</summary>
    public sealed class AccountService
    {
        private readonly IAccountApi _api;
        private readonly IAuthCredentialStore _credentials;
        private readonly ISceneNavigator _nav;
        private readonly IReadOnlyList<ISocialSignIn> _social;

        public AccountService(IAccountApi api, IAuthCredentialStore credentials, ISceneNavigator nav, IReadOnlyList<ISocialSignIn> social)
        {
            _api = api;
            _credentials = credentials;
            _nav = nav;
            _social = social ?? Array.Empty<ISocialSignIn>();
        }

        public bool IsGuest => !_credentials.HasCredential || _credentials.Method == LoginMethod.Guest;

        /// <summary>게스트로 로그인했을 때 서버가 정한 게스트 ID. 외부 계정이거나 로그인 정보가 없으면 null</summary>
        public string GuestId => _credentials.HasCredential && _credentials.Method == LoginMethod.Guest ? _credentials.AccountId : null;

        /// <summary>게스트 계정을 첫 번째로 쓸 수 있는 외부 계정에 연결한다. 이미 연결된 계정이면 아무것도 하지 않는다</summary>
        public async UniTask<LinkResult> LinkAsync()
        {
            if (!IsGuest)
            {
                return LinkResult.AlreadyLinked;
            }

            foreach (var social in _social)
            {
                if (!social.IsAvailable)
                {
                    continue;
                }

                string accountId;
                try
                {
                    accountId = await social.SignInAsync();
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                    return LinkResult.Cancelled;
                }

                if (accountId == null)
                {
                    return LinkResult.Cancelled;
                }

                await _api.LinkAccount(social.Method, accountId);
                _credentials.Save(social.Method, AuthCredentials.IdToSave(social.Method, accountId));
                return LinkResult.Linked;
            }

            return LinkResult.Unavailable;
        }

        /// <summary>로그아웃한다. 게스트면 계정 데이터도 지운다</summary>
        public async UniTask LogoutAsync()
        {
            if (IsGuest)
            {
                await _api.DeleteAccount();
            }

            await ReturnToLoginAsync();
        }

        public async UniTask DeleteAccountAsync()
        {
            await _api.DeleteAccount();
            await ReturnToLoginAsync();
        }

        private UniTask ReturnToLoginAsync()
        {
            _credentials.Clear();
            return _nav.GoToLoading();
        }
    }
}
