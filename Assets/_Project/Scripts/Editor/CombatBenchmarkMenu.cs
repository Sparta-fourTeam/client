using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>고정 부하 측정 씬 생성과 전용 iOS 개발 빌드. 일반 게임 세이브와 별도 앱 ID를 사용한다.</summary>
    public static class CombatBenchmarkMenu
    {
        public const string ScenePath = "Assets/_Project/Scenes/Sandbox/CombatBenchmark.unity";
        private const string RootMenu = "Tools/Project Nova/Combat Benchmark/";

        [MenuItem(RootMenu + "Create Scene")]
        public static void CreateScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { throw new InvalidOperationException("Stop Play Mode first"); }
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) { return; }
            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Sandbox/SkillSandbox.unity");
            foreach (var root in scene.GetRootGameObjects())
            {
                bool light = root.GetComponentsInChildren<Component>(true).Any(c => c != null && c.GetType().Name == "Light2D");
                if (root.GetComponentInChildren<Camera>(true) == null && !light) { UnityEngine.Object.DestroyImmediate(root); }
            }
            var camera = Camera.main;
            if (camera == null) { camera = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>(); camera.tag = "MainCamera"; }
            camera.orthographic = true; camera.orthographicSize = 5.5f; camera.transform.position = new Vector3(0, 2.4f, -10);
            var benchmarkType = Type.GetType("Game.Sandbox.CombatBenchmark, Game.Sandbox", true);
            var runner = new GameObject("CombatBenchmark").AddComponent(benchmarkType);
            var serialized = new SerializedObject(runner);
            serialized.FindProperty("enemyPrefab").objectReferenceValue = FindComponent("Assets/_Project/Prefabs/Stage/Golem.prefab", "Enemy");
            serialized.FindProperty("projectilePrefab").objectReferenceValue = FindComponent("Assets/_Project/Prefabs/Skills/Arrow.prefab", "Projectile");
            serialized.FindProperty("hitscanPrefab").objectReferenceValue = FindComponent("Assets/_Project/Prefabs/Skills/Lightning.prefab", "HitscanEffect");
            serialized.FindProperty("areaPrefab").objectReferenceValue = FindComponent("Assets/_Project/Prefabs/Skills/FrostPrison.prefab", "AreaZone");
            serialized.FindProperty("beamPrefab").objectReferenceValue = FindComponent("Assets/_Project/Prefabs/Skills/SunBeam.prefab", "AreaZone");
            serialized.FindProperty("chainPrefab").objectReferenceValue = FindComponent("Assets/_Project/Prefabs/Skills/LightningChain.prefab", "ChainBolt");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
        }

        private static Component FindComponent(string path, string type)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { throw new InvalidOperationException("Missing prefab: " + path); }
            var component = prefab.GetComponentsInChildren<Component>(true).FirstOrDefault(c => c != null && c.GetType().Name == type);
            return component != null ? component : throw new InvalidOperationException(path + " has no " + type);
        }

        [MenuItem(RootMenu + "Run in Editor")]
        public static void RunInEditor()
        {
            if (!File.Exists(ScenePath)) { CreateScene(); }
            EditorSceneManager.OpenScene(ScenePath);
            var previous = EditorSceneManager.playModeStartScene;
            var previousPreloads = PlayerSettings.GetPreloadedAssets();
            PlayerSettings.SetPreloadedAssets(previousPreloads.Where(asset => asset == null || asset.GetType().FullName != "VContainer.Unity.VContainerSettings").ToArray());
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            EditorApplication.playModeStateChanged += Restore;
            EditorApplication.EnterPlaymode();
            void Restore(PlayModeStateChange state)
            {
                if (state != PlayModeStateChange.EnteredEditMode) { return; }
                EditorSceneManager.playModeStartScene = previous;
                PlayerSettings.SetPreloadedAssets(previousPreloads);
                EditorApplication.playModeStateChanged -= Restore;
            }
        }

        [MenuItem(RootMenu + "Build iOS Development")]
        public static void BuildIOS()
        {
            string label = Environment.GetEnvironmentVariable("NOVA_BENCHMARK_LABEL") ?? "after";
            string output = Environment.GetEnvironmentVariable("NOVA_BENCHMARK_BUILD") ?? Path.GetFullPath("Builds/CombatBenchmark-" + label);
            CreateScene();
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var runnerType = Type.GetType("Game.Sandbox.CombatBenchmark, Game.Sandbox", true);
            var runner = UnityEngine.Object.FindAnyObjectByType(runnerType);
            var serialized = new SerializedObject(runner);
            serialized.FindProperty("runLabel").stringValue = label;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene);
            var oldPreloads = PlayerSettings.GetPreloadedAssets();
            string oldId = PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS);
            string oldTeam = PlayerSettings.iOS.appleDeveloperTeamID;
            bool oldSigning = PlayerSettings.iOS.appleEnableAutomaticSigning;
            bool oldDevelopment = EditorUserBuildSettings.development;
            bool oldAttach = EditorUserBuildSettings.connectProfiler;
            try
            {
                PlayerSettings.SetPreloadedAssets(oldPreloads.Where(asset => asset == null || asset.GetType().FullName != "VContainer.Unity.VContainerSettings").ToArray());
                // 실기기의 실제 게임 데이터와 분리. 전후는 같은 ID로 교체 설치한다.
                PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS, "com.teamdoodle.combatbenchmark");
                PlayerSettings.iOS.appleDeveloperTeamID = Environment.GetEnvironmentVariable("NOVA_BENCHMARK_TEAM") ?? oldTeam;
                PlayerSettings.iOS.appleEnableAutomaticSigning = true;
                EditorUserBuildSettings.development = true;
                EditorUserBuildSettings.connectProfiler = false;
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = output,
                    target = BuildTarget.iOS,
                    options = BuildOptions.Development
                });
                Directory.CreateDirectory(output);
                File.WriteAllText(Path.Combine(output, "benchmark-build.json"), JsonUtility.ToJson(new BuildManifest
                {
                    label = label,
                    utc = DateTime.UtcNow.ToString("O"),
                    unity = Application.unityVersion,
                    result = report.summary.result.ToString(),
                    errors = report.summary.totalErrors,
                    profiling = "Development; no AutoConnectProfiler; callstack recording off unless attached externally"
                }, true));
                if (report.summary.result != BuildResult.Succeeded) { throw new InvalidOperationException("Benchmark build failed: " + report.summary.result); }
            }
            finally
            {
                PlayerSettings.SetPreloadedAssets(oldPreloads);
                PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS, oldId);
                PlayerSettings.iOS.appleDeveloperTeamID = oldTeam;
                PlayerSettings.iOS.appleEnableAutomaticSigning = oldSigning;
                EditorUserBuildSettings.development = oldDevelopment;
                EditorUserBuildSettings.connectProfiler = oldAttach;
                AssetDatabase.SaveAssets();
            }
        }
        [Serializable]
        private sealed class BuildManifest
        {
            public string label, utc, unity, result, profiling;
            public int errors;
        }
    }
}
