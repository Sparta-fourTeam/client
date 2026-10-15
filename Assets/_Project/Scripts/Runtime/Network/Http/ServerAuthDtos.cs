using System;

namespace Game.Network
{
    /// <summary>POST /api/auth/social/login 요청. provider는 GOOGLE 또는 APPLE이고 idToken은 외부 로그인으로 받은 ID 토큰 원문이다</summary>
    [Serializable]
    public class SocialLoginRequest
    {
        public string provider;
        public string idToken;
    }

    /// <summary>POST /api/auth/guest/login 요청. guestId는 가입 때 받은 게스트 ID다</summary>
    [Serializable]
    public class GuestLoginRequest
    {
        public string guestId;
    }

    /// <summary>POST /api/auth/refresh 요청. 쓴 리프레시 토큰은 서버가 무효로 한다</summary>
    [Serializable]
    public class RefreshRequest
    {
        public string refreshToken;
    }

    /// <summary>로그인·재발급 응답의 토큰 한 쌍</summary>
    [Serializable]
    public class AuthTokens
    {
        public string accessToken;
        public string refreshToken;
    }

    /// <summary>POST /api/auth/guest/signup 응답. guestId는 이 응답에서만 내려가므로 받자마자 저장해야 한다</summary>
    [Serializable]
    public class GuestSignUpResponse
    {
        public string guestId;
        public AuthTokens tokens;
    }
}
