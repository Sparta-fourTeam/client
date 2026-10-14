namespace Game.Core
{
    /// <summary>기기에 저장한 로그인 정보(방식과 계정 ID). 다음 실행에서 로그인창 없이 바로 로그인하는 데 쓴다.
    /// 게스트는 서버가 정해 준 게스트 ID를 보관해 IAuthApi.Login에 넘긴다. 외부 계정은 실행할 때마다 다시 로그인해 토큰을 받는다</summary>
    public interface IAuthCredentialStore
    {
        /// <summary>저장된 로그인 정보가 있는지. 최초 진입이면 false</summary>
        bool HasCredential { get; }

        /// <summary>저장된 로그인 방식. 로그인 정보가 없으면 의미 없다</summary>
        LoginMethod Method { get; }

        /// <summary>저장된 계정 ID(게스트 ID 또는 외부 계정 ID). 없으면 null</summary>
        string AccountId { get; }

        void Save(LoginMethod method, string accountId);

        void Clear();
    }
}
