namespace Game.Core
{
    /// <summary>기기에 저장하는 로그인 정보의 계정 ID 규칙</summary>
    public static class AuthCredentials
    {
        /// <summary>외부 계정(구글·애플)으로 로그인했을 때 저장하는 값. 외부 계정은 실행할 때마다 다시 로그인해 새 토큰을 받으므로,
        /// 오래되어 쓸 수 없고 민감한 ID 토큰을 기기에 남기지 않고 방식만 기억한다</summary>
        public const string SocialAccountId = "social";

        /// <summary>저장할 계정 ID. 게스트는 서버가 정한 게스트 ID를, 외부 계정은 SocialAccountId를 쓴다</summary>
        public static string IdToSave(LoginMethod method, string accountId) =>
            method == LoginMethod.Guest ? accountId : SocialAccountId;
    }
}
