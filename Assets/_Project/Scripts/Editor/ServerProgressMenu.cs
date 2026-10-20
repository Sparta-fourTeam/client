using Game.Network;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>에디터에서 다음 재생에 사용할 서버 진행을 선택한다. 서버 인증이 꺼져 있으면 켜도 서버 진행을 쓰지 않는다.</summary>
    public static class ServerProgressMenu
    {
        private const string MenuPath = "Tools/Project Nova/Server Progress";

        [MenuItem(MenuPath, priority = 12)]
        private static void Toggle()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { return; }
            bool enabled = !EditorPrefs.GetBool(ServerConfig.EditorProgressPreferenceKey, false);
            EditorPrefs.SetBool(ServerConfig.EditorProgressPreferenceKey, enabled);
            Menu.SetChecked(MenuPath, enabled);
            Debug.Log(enabled
                ? "[Server Progress] 켬: Server Authentication도 켜져 있어야 적용됩니다. Boot부터 재생하세요."
                : "[Server Progress] 끔: 다음 재생부터 로컬 진행을 사용합니다.");
        }

        [MenuItem(MenuPath, validate = true)]
        private static bool Validate()
        {
            Menu.SetChecked(MenuPath, EditorPrefs.GetBool(ServerConfig.EditorProgressPreferenceKey, false));
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }
    }
}
