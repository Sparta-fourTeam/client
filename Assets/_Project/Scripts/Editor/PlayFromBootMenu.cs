using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>재생 버튼을 눌렀을 때 열어 둔 씬이 아니라 Boot 씬부터 시작할지 고르는 토글 메뉴.
    /// Unity 에디터에는 이 값(EditorSceneManager.playModeStartScene)을 바꾸는 기본 메뉴가 없다.
    /// Stage 씬은 로비에서 BattleLauncher가 전투를 발급받은 뒤에 들어와야 StageContext가 채워지므로,
    /// 실제 흐름을 확인할 때는 Boot부터 재생한다. 체크 표시는 실제 값을 읽어서 그리므로 스크립트로 바꿔도 맞게 보인다</summary>
    public static class PlayFromBootMenu
    {
        private const string MenuPath = "Tools/Project Nova/Play From Boot";
        private const string BootScenePath = "Assets/_Project/Scenes/Boot.unity";

        private static bool IsOn
        {
            get
            {
                SceneAsset start = EditorSceneManager.playModeStartScene;
                return start != null && AssetDatabase.GetAssetPath(start) == BootScenePath;
            }
        }

        [MenuItem(MenuPath, priority = 10)]
        private static void Toggle()
        {
            if (IsOn)
            {
                EditorSceneManager.playModeStartScene = null;
                Debug.Log("[PlayFromBoot] 끔: 재생하면 지금 열린 씬에서 시작합니다");
                return;
            }

            var boot = AssetDatabase.LoadAssetAtPath<SceneAsset>(BootScenePath);
            if (boot == null)
            {
                Debug.LogWarning($"[PlayFromBoot] Boot 씬을 찾지 못했습니다: {BootScenePath}");
                return;
            }

            EditorSceneManager.playModeStartScene = boot;
            Debug.Log("[PlayFromBoot] 켬: 재생하면 열린 씬과 상관없이 Boot부터 시작합니다");
        }

        [MenuItem(MenuPath, validate = true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, IsOn);
            return true;
        }
    }
}
