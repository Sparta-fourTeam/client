namespace Game.Core
{
    /// <summary>기기에 저장한 로그인 정보(방식과 계정 ID). 다음 실행에서 로그인창 없이 바로 로그인하는 데 쓴다.
    /// TODO(server): 자격 증명 형식(server#1: 게스트 uuid 단독 vs guestId+guestSecret, 소셜 토큰)이 정해지면 그 값을 보관하고 IAuthApi.Login에 넘긴다 (#180)</summary>
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
