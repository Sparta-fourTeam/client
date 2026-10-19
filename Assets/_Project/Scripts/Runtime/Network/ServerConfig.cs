using UnityEngine;

namespace Game.Network
{
    /// <summary>서버 주소와 서버 인증을 쓸지 정한다.
    /// Android는 개발·릴리스 모두 서버 인증을 쓴다. 에디터는 가짜 구글 로그인이라 기본으로 끈다.
    /// 개발 override는 플랫폼 기본값보다 우선한다. 서버 진행 연결은 별도 opt-in이다.</summary>
    public static class ServerConfig
    {
        /// <summary>서버 주소. 요청 경로가 api/로 시작하므로 끝에 /api나 /auth를 붙이지 않는다
        /// (https://doodle-defense.duckdns.org/auth는 웹사이트 화면이다)</summary>
        public const string BaseUrl = "https://doodle-defense.duckdns.org";

        // 개발 중에 켜고 끄는 값. "1"이면 켜고 "0"이면 끈다. 없으면 아래 기본 규칙을 따른다
        private const string OverrideKey = "dev.server_auth";
        private const string ProgressOverrideKey = "dev.server_progress";

        // 서버 인증을 쓰는 앱은 진행도 같은 계정의 서버에서 읽는다. 에디터는 인증을
        // 명시적으로 켰을 때만 통합 경로를 시험한다. 서버 원본 미게시/장애 시 진입이 실패한다.
        public static bool UseServerProgress => UseServerAuth
            && PlayerPrefs.GetString(ProgressOverrideKey, string.Empty) == "1";
        public static bool UseServerPlayer => UseServerProgress;

        public static bool UseServerAuth => ResolveServerAuth(Application.platform, PlayerPrefs.GetString(OverrideKey, string.Empty));

        /// <summary>개발 override를 우선하고, 없으면 Android에서 서버 인증·계정 API를 선택한다.</summary>
        public static bool ResolveServerAuth(RuntimePlatform platform, string preference)
        {
            if (preference == "1") { return true; }
            if (preference == "0") { return false; }
            return platform == RuntimePlatform.Android;
        }
    }
}
