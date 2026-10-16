using UnityEngine;

namespace Game.Network
{
    /// <summary>서버 주소와 서버 인증을 쓸지 정한다.
    /// Android는 개발·릴리스 모두 서버 인증을 쓴다. 에디터는 가짜 구글 로그인이라 기본으로 끈다.
    /// 개발 override는 플랫폼 기본값보다 우선하며, 서버 플레이어 API 선택과는 별개다.</summary>
    public static class ServerConfig
    {
        /// <summary>서버 주소. 요청 경로가 api/로 시작하므로 끝에 /api나 /auth를 붙이지 않는다
        /// (https://doodle-defense.duckdns.org/auth는 웹사이트 화면이다)</summary>
        public const string BaseUrl = "https://doodle-defense.duckdns.org";

        // 개발 중에 켜고 끄는 값. "1"이면 켜고 "0"이면 끈다. 없으면 아래 기본 규칙을 따른다
        private const string OverrideKey = "dev.server_auth";

        // 플레이어 상태를 서버(GET /api/user/me)에서 받을지. 서버 PR #47이 배포되기 전에는 요청이 실패하므로 기본은 끔이고,
        // 개발 중에 PlayerPrefs의 dev.server_player를 "1"로 두어야 켠다
        private const string PlayerOverrideKey = "dev.server_player";

        public const string UpgradeTestKey = "dev.server_upgrade_test";

        /// <summary>기본은 꺼짐. 릴리스 빌드에서는 저장된 개발 설정이 있어도 켜지지 않는다. 변경 후 Boot부터 다시 시작한다.</summary>
        public static bool UseServerUpgradeTest
        {
            get
            {
                return (Application.isEditor || Debug.isDebugBuild)
                    && PlayerPrefs.GetString(UpgradeTestKey, string.Empty) == "1";
            }
        }

        public static bool UseServerPlayer => PlayerPrefs.GetString(PlayerOverrideKey, string.Empty) == "1";

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
