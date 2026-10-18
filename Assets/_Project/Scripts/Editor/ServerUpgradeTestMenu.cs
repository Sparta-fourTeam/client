using Game.Network;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public static class ServerUpgradeTestMenu
    {
        private const string Path = "Tools/Project Nova/Server Upgrade Test Mode";
        [MenuItem(Path)]
        private static void Toggle()
        {
            PlayerPrefs.SetString(ServerConfig.UpgradeTestKey, ServerConfig.UseServerUpgradeTest ? "0" : "1");
            PlayerPrefs.Save();
            Debug.Log("서버 강화 테스트 모드를 변경했습니다. Boot부터 재생하세요. 서버 조회·단건 강화·별 보상·에너지 구매를 사용하며 기존 Local 저장은 유지합니다.");
        }
        [MenuItem(Path, true)]
        private static bool Validate()
        {
            Menu.SetChecked(Path, ServerConfig.UseServerUpgradeTest);
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }
    }
}
