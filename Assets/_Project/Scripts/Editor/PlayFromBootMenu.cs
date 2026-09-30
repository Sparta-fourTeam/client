using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>재생 버튼을 눌렀을 때 열어 둔 씬이 아니라 Boot 씬부터 시작할지 고르는 토글 메뉴.
    /// Unity 에디터에는 이 값(EditorSceneManager.playModeStartScene)을 바꾸는 기본 메뉴가 없다.
    /// Stage 씬은 로비에서 BattleLauncher가 전투를 발급받은 뒤에 들어와야 StageContext가 채워지므로,
    /// 실제 흐름을 확인할 때는 Boot부터 재생한다. 체크 표시는 실제 값을 읽어서 그리므로 스크립트로 바꿔도 맞게 보인다.
    /// playModeStartScene은 에디터를 다시 켜면 디스크에서 복원되지 않아(UserSettings, Library, EditorPrefs 어디에도 저장되지 않는다)
    /// 켜 둔 채 닫았다가 열면 조용히 꺼진다. 그래서 켜고 끈 선택을 EditorPrefs에 따로 저장하고 로드 때 복원한다</summary>
    [InitializeOnLoad]
    public static class PlayFromBootMenu
    {
        private const string MenuPath = "Tools/Project Nova/Play From Boot";
        private const string BootScenePath = "Assets/_Project/Scenes/Boot.unity";

        // EditorPrefs는 컴퓨터 전체에서 공유되므로 프로젝트 폴더별로 키를 나눈다 (같은 프로젝트를 폴더 두 곳에 두고 쓸 때 서로 영향을 주지 않게)
        private static string PrefKey => $"ProjectNova.PlayFromBoot.{Hash128.Compute(Application.dataPath)}";

        static PlayFromBootMenu()
        {
            // 에셋 DB가 준비된 뒤에 복원한다. 정적 생성자 시점에는 씬 에셋을 아직 못 읽을 수 있다
            EditorApplication.delayCall += Restore;
        }

        private static bool IsOn
        {
            get
            {
                SceneAsset start = EditorSceneManager.playModeStartScene;
                return start != null && AssetDatabase.GetAssetPath(start) == BootScenePath;
            }
        }

        /// <summary>저장된 선택이 "켬"인데 실제 값이 풀려 있으면 다시 켠다. "끔"이면 건드리지 않는다
        /// (다른 스크립트가 잠깐 켜 둔 값을 함부로 지우지 않으려고)</summary>
        private static void Restore()
        {
            if (EditorPrefs.GetBool(PrefKey, false) && !IsOn)
            {
                Apply(true, log: false);
            }
        }

        private static bool Apply(bool on, bool log)
        {
            if (!on)
            {
                EditorSceneManager.playModeStartScene = null;
                EditorPrefs.SetBool(PrefKey, false);
                if (log)
                {
                    Debug.Log("[PlayFromBoot] 끔: 재생하면 지금 열린 씬에서 시작합니다");
                }

                return true;
            }

            var boot = AssetDatabase.LoadAssetAtPath<SceneAsset>(BootScenePath);
            if (boot == null)
            {
                Debug.LogWarning($"[PlayFromBoot] Boot 씬을 찾지 못했습니다: {BootScenePath}");
                return false;
            }

            EditorSceneManager.playModeStartScene = boot;
            EditorPrefs.SetBool(PrefKey, true);
            if (log)
            {
                Debug.Log("[PlayFromBoot] 켬: 재생하면 열린 씬과 상관없이 Boot부터 시작합니다 (에디터를 다시 켜도 유지됩니다)");
            }

            return true;
        }

        [MenuItem(MenuPath, priority = 10)]
        private static void Toggle()
        {
            Apply(!IsOn, log: true);
        }

        [MenuItem(MenuPath, validate = true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, IsOn);
            return true;
        }
    }
}
