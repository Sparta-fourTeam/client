using System.Collections.Generic;

namespace Game.Network
{
    /// <summary>서버 로그인으로 받은 토큰을 메모리에 들고 있다가 요청마다 Authorization 헤더로 붙인다.
    /// 앱을 다시 켜면 비워지고 로그인에서 다시 채운다.
    /// TODO(server): 액세스 토큰이 만료되면 refresh 토큰으로 POST /api/auth/refresh를 불러 갱신한다. 지금은 서버에 인증이 필요한 API를 쓰지 않아 붙이지 않았다</summary>
    public sealed class AuthSession : IAuthHeaderProvider
    {
        private static readonly IReadOnlyDictionary<string, string> NoHeaders = new Dictionary<string, string>();

        public string AccessToken { get; private set; }
        public string RefreshToken { get; private set; }

        public bool IsSignedIn => !string.IsNullOrEmpty(AccessToken);

        public void Set(string accessToken, string refreshToken)
        {
            AccessToken = accessToken;
            RefreshToken = refreshToken;
        }

        public void Clear() => Set(null, null);

        public IReadOnlyDictionary<string, string> GetHeaders() =>
            IsSignedIn ? new Dictionary<string, string> { ["Authorization"] = $"Bearer {AccessToken}" } : NoHeaders;
    }
}
