using UnityEngine;

namespace Game.Network
{
    /// <summary>서버 주소와 인증 백엔드 선택.
    /// Android 릴리스는 서버 인증을 사용하며 개발용 로컬 전환 값으로 끌 수 없다.
    /// 에디터의 구글 로그인은 가짜이므로 기본적으로 로컬 인증을 사용한다.</summary>
    public static class ServerConfig
    {
        /// <summary>서버 주소. 요청 경로가 api/로 시작하므로 끝에 /api나 /auth를 붙이지 않는다
        /// (https://doodle-defense.duckdns.org/auth는 웹사이트 화면이다)</summary>
        public const string BaseUrl = "https://doodle-defense.duckdns.org";

        // 개발·에디터에서만 적용하는 값. Android 릴리스는 이 값과 관계없이 서버 인증을 쓴다.
        private const string OverrideKey = "dev.server_auth";

        public static bool UseServerAuth => SelectServerAuth(Application.platform, Debug.isDebugBuild,
            PlayerPrefs.GetString(OverrideKey, string.Empty));

        public static bool SelectServerAuth(RuntimePlatform platform, bool development, string preference)
        {
            if (platform == RuntimePlatform.Android && !development) { return true; }
            if (preference == "1") { return true; }
            if (preference == "0") { return false; }
            return platform == RuntimePlatform.Android && development;
        }
    }
}
