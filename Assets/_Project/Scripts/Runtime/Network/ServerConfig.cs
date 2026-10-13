using UnityEngine;

namespace Game.Network
{
    /// <summary>서버 주소와 서버 인증을 쓸지 정한다.
    /// 서버 인증은 Android 개발 빌드에서만 켠다. 에디터는 가짜 구글 로그인이라 서버가 ID 토큰을 검증하지 못하고,
    /// 릴리스 빌드는 기기 확인이 끝난 뒤에 켠다.
    /// TODO(server): 기기에서 서버 로그인을 확인하면 릴리스 빌드에서도 켠다 (주소가 HTTPS라 릴리스에서도 연결된다)</summary>
    public static class ServerConfig
    {
        /// <summary>서버 주소. 요청 경로가 api/로 시작하므로 끝에 /api나 /auth를 붙이지 않는다
        /// (https://doodle-defense.duckdns.org/auth는 웹사이트 화면이다)</summary>
        public const string BaseUrl = "https://doodle-defense.duckdns.org";

        // 개발 중에 켜고 끄는 값. "1"이면 켜고 "0"이면 끈다. 없으면 아래 기본 규칙을 따른다
        private const string OverrideKey = "dev.server_auth";

        public static bool UseServerAuth
        {
            get
            {
                string value = PlayerPrefs.GetString(OverrideKey, string.Empty);
                if (value == "1") { return true; }
                if (value == "0") { return false; }
                return Application.platform == RuntimePlatform.Android && Debug.isDebugBuild;
            }
        }
    }
}
