using UnityEditor;
using UnityEditor.SceneManagement;

namespace Game.Editor
{
    /// <summary>스킬 샌드박스 씬을 열고 바로 재생한다. 샌드박스는 Boot 씬을 거치지 않는 독립 씬이라 Play From Boot와 상관없이
    /// 항상 이 씬에서 시작한다(재생을 마치면 원래 시작 씬 설정으로 돌아간다).</summary>
    public static class SkillSandboxMenu
    {
        private const string ScenePath = "Assets/_Project/Scenes/Sandbox/SkillSandbox.unity";

        private const string MenuPath = "Tools/Project Nova/Skill Sandbox";

        // 이미 재생 중이거나 재생으로 바뀌는 중에는 씬을 열 수 없으므로 메뉴를 막는다.
        [MenuItem(MenuPath, true)]
        private static bool CanOpen() => !EditorApplication.isPlayingOrWillChangePlaymode;

        [MenuItem(MenuPath)]
        private static void Open()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) { return; }
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            // Play From Boot가 켜져 있으면 재생이 Boot 씬에서 시작하므로, 이번 재생만 샌드박스 씬에서 시작하게 한다.
            var previous = EditorSceneManager.playModeStartScene;
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            EditorApplication.EnterPlaymode();
            EditorApplication.playModeStateChanged += Restore;

            void Restore(PlayModeStateChange change)
            {
                if (change != PlayModeStateChange.EnteredEditMode) { return; }
                EditorSceneManager.playModeStartScene = previous;
                EditorApplication.playModeStateChanged -= Restore;
            }
        }
    }
}
