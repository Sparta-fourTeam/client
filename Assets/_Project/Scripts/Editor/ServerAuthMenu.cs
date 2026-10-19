using Game.Network;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>에디터에서 다음 재생에 사용할 서버 인증을 선택한다.</summary>
    public static class ServerAuthMenu
    {
        private const string MenuPath = "Tools/Project Nova/Server Authentication";

        [MenuItem(MenuPath, priority = 11)]
        private static void Toggle()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { return; }
            bool enabled = !ServerConfig.UseServerAuth;
            EditorPrefs.SetBool(ServerConfig.EditorAuthPreferenceKey, enabled);
            Menu.SetChecked(MenuPath, enabled);
            Debug.Log(enabled
                ? "[Server Authentication] 켬: Boot부터 재생한 뒤 게스트로 로그인하세요. 에디터 구글 로그인은 서버에서 사용할 수 없습니다."
                : "[Server Authentication] 끔: 다음 재생부터 로컬 인증을 사용합니다.");
        }

        [MenuItem(MenuPath, validate = true)]
        private static bool Validate()
        {
            Menu.SetChecked(MenuPath, ServerConfig.UseServerAuth);
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }
    }
}
