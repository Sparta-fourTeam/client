using System.Collections.Generic;

namespace Game.Network
{
    /// <summary>서버 로그인 토큰을 메모리에 보관한다. 로그인/초기화는 세대를 바꾸며,
    /// refresh는 시작한 세대에만 결과를 반영해 로그아웃이나 새 로그인을 되돌리지 않는다.</summary>
    public sealed class AuthSession : IAuthTokenStore
    {
        private static readonly IReadOnlyDictionary<string, string> NoHeaders = new Dictionary<string, string>();
        private readonly object _sync = new();
        private string _accessToken, _refreshToken;
        private long _generation;

        public string AccessToken { get { lock (_sync) { return _accessToken; } } }
        public string RefreshToken { get { lock (_sync) { return _refreshToken; } } }
        public long Generation { get { lock (_sync) { return _generation; } } }
        public bool IsSignedIn => !string.IsNullOrEmpty(AccessToken);

        public void Set(string accessToken, string refreshToken)
        {
            lock (_sync)
            {
                _generation++;
                _accessToken = accessToken;
                _refreshToken = refreshToken;
            }
        }

        public void Clear() => Set(null, null);

        public bool TryRefresh(long generation, string accessToken, string refreshToken)
        {
            lock (_sync)
            {
                if (_generation != generation || string.IsNullOrEmpty(_accessToken)) { return false; }
                _accessToken = accessToken;
                _refreshToken = refreshToken;
                return true;
            }
        }

        public bool TryClear(long generation)
        {
            lock (_sync)
            {
                if (_generation != generation) { return false; }
                _generation++;
                _accessToken = _refreshToken = null;
                return true;
            }
        }

        public IReadOnlyDictionary<string, string> GetHeaders()
        {
            lock (_sync)
            {
                return string.IsNullOrEmpty(_accessToken) ? NoHeaders
                    : new Dictionary<string, string> { ["Authorization"] = $"Bearer {_accessToken}" };
            }
        }
    }
}
