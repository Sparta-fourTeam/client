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
        /// <summary>서버가 연동을 거절했다(409). 이 계정이 이미 외부 계정과 연동됐거나, 그 외부 계정이 이미 다른 계정에 연동돼 있다</summary>
        Conflict,
        /// <summary>서버 오류·네트워크 문제 등으로 연동하지 못했다. 로그인 정보는 바꾸지 않았으니 다시 시도할 수 있다</summary>
        Failed,
        /// <summary>서버 인증이 만료됐다. 기존 계정 정보를 유지한 채 다시 로그인해야 한다</summary>
        ReauthenticationRequired,
    }

    /// <summary>로그아웃·계정 삭제·계정 연동. 게스트는 로그아웃하면 계정 데이터도 함께 지운다.
    /// 로그아웃과 삭제는 저장된 로그인 정보를 지우고 Loading 씬으로 돌아가 로그인창부터 다시 시작한다</summary>
    public sealed class AccountService
    {
        private readonly IAccountApi _api;
        private readonly IBattleCheckpointStore _checkpoints;
        private readonly IAuthCredentialStore _credentials;
        private readonly ISceneNavigator _nav;
        private readonly IReadOnlyList<ISocialSignIn> _social;
        private readonly PlayerProfile _profile;
        private readonly ILoginConsentStore _consents;
        private readonly IAnalyticsCollectionControl _analytics;

        public AccountService(IAccountApi api, IAuthCredentialStore credentials, ISceneNavigator nav, IReadOnlyList<ISocialSignIn> social,
            PlayerProfile profile = null, IBattleCheckpointStore checkpoints = null,
            ILoginConsentStore consents = null, IAnalyticsCollectionControl analytics = null)
        {
            _consents = consents;
            _analytics = analytics;
            _checkpoints = checkpoints;
            _api = api;
            _credentials = credentials;
            _nav = nav;
            _social = social ?? Array.Empty<ISocialSignIn>();
            _profile = profile;
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

                // 서버가 받아들인 뒤에만 로그인 정보를 바꾼다. 거절되거나 실패하면 게스트 그대로 둔다
                try
                {
                    await _api.LinkAccount(social.Method, accountId);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (ApiException ex)
                {
                    Debug.LogException(ex);
                    if (ex.Code == "SESSION_EXPIRED" || ex.Code == "HTTP_401") { return LinkResult.ReauthenticationRequired; }
                    return ex.Kind == ApiErrorKind.Rejected && ex.Code == "HTTP_409" ? LinkResult.Conflict : LinkResult.Failed;
                }

                _credentials.Save(social.Method, AuthCredentials.IdToSave(social.Method, accountId));
                return LinkResult.Linked;
            }

            return LinkResult.Unavailable;
        }

        /// <summary>저장이 성공한 뒤 표시 이름만 바꾸고 프로필 구독자에게 알린다. ID·진행·로그인 방식은 유지한다</summary>
        public async UniTask ChangeNicknameAsync(string nickname)
        {
            if (!NicknameRules.IsValid(nickname)) { throw new ApiException(ApiErrorKind.Rejected, "INVALID_NICKNAME"); }
            if (_profile == null || !_profile.IsLoaded) { throw new InvalidOperationException("플레이어 정보를 먼저 불러와야 합니다."); }
            string saved = await _api.ChangeNickname(nickname);
            var snapshot = _profile.Snapshot();
            snapshot.nickname = saved;
            _profile.Apply(snapshot);
        }

        /// <summary>로그아웃한다. 게스트면 계정 데이터도 지운다</summary>
        public async UniTask LogoutAsync()
        {
            bool deleted = IsGuest;
            if (IsGuest)
            {
                await _api.DeleteAccount();
            }

            await ReturnToLoginAsync(deleted);
        }

        public async UniTask DeleteAccountAsync()
        {
            await _api.DeleteAccount();
            await ReturnToLoginAsync(true);
        }

        /// <summary>계정과 진행을 지우지 않고 기존 로그인 방식·게스트 ID로 인증 절차를 다시 밟는다.</summary>
        public UniTask ReauthenticateAsync() => _nav.GoToLoading();

        private UniTask ReturnToLoginAsync(bool deleted)
        {
            _analytics?.SetCollectionAllowed(false);
            _consents?.Clear();
            if (deleted || _checkpoints is not IAccountCheckpointRetention) { _checkpoints?.Clear(); }
            _credentials.Clear();
            // 서버 데이터는 계정에 남아 있어도 이 기기의 화면 캐시는 다음 계정에 노출하면 안 된다.
            _profile?.Clear();
            return _nav.GoToLoading();
        }
    }
}
