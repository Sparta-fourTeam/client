using System;

namespace Game.Core
{
    /// <summary>로그인 응답</summary>
    [Serializable]
    public class LoginResponse
    {
        public string accessToken;

        /// <summary>액세스 토큰을 다시 받는 데 쓰는 토큰. 서버가 주지 않으면(Local) null</summary>
        public string refreshToken;

        /// <summary>다음 로그인에 쓸 계정 ID. 서버가 게스트 가입 때 정해 준 게스트 ID다. 정해 주지 않으면 null</summary>
        public string accountId;
    }
}
