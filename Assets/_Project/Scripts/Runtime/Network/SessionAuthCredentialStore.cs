using Game.Core;

namespace Game.Network
{
    /// <summary>로그아웃/삭제로 저장 자격 정보를 지울 때 진행 중인 refresh와 메모리 세션도 무효로 만든다.</summary>
    public sealed class SessionAuthCredentialStore : IAuthCredentialStore
    {
        private readonly IAuthCredentialStore _inner;
        private readonly AuthSession _session;

        public SessionAuthCredentialStore(IAuthCredentialStore inner, AuthSession session)
        {
            _inner = inner;
            _session = session;
        }

        public bool HasCredential => _inner.HasCredential;
        public LoginMethod Method => _inner.Method;
        public string AccountId => _inner.AccountId;
        public void Save(LoginMethod method, string accountId) => _inner.Save(method, accountId);

        public void Clear()
        {
            _session.Clear();
            _inner.Clear();
        }
    }
}
