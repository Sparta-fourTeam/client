using System;
using System.IO;
using System.Linq;
using Game.Boot;
using Game.Core;
using Game.Core.Defense;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace Game.Editor
{
    public sealed class PlaytestWindow : EditorWindow
    {
        private const string Layout = "Assets/_Project/Scripts/Editor/Tools/Playtest.uxml";
        private double _nextRefresh;

        [MenuItem("Tools/Project Nova/Playtest", priority = 30)]
        public static void Open() => GetWindow<PlaytestWindow>("Playtest");

        private void OnEnable() => EditorApplication.update += Tick;
        private void OnDisable() => EditorApplication.update -= Tick;

        private void Tick()
        {
            if (EditorApplication.timeSinceStartup < _nextRefresh)
            {
                return;
            }

            _nextRefresh = EditorApplication.timeSinceStartup + 0.3;
            Refresh();
        }

        public void CreateGUI()
        {
            minSize = new Vector2(420, 420);
            rootVisualElement.Clear();
            AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Layout).CloneTree(rootVisualElement);
            Button("breakWallButton", () => { var wall = ActiveWall(); if (wall != null) { wall.TakeDamage(wall.CurrentHp); } });
            Button("failButton", () => { var fault = Resolve<ISubmitFaultSwitch>(); if (fault != null) { fault.FailNextSubmits = StageManager.AutoRetryCount + 1; } });
            Button("clearFaultButton", () => { var fault = Resolve<ISubmitFaultSwitch>(); if (fault != null) { fault.FailNextSubmits = 0; } });
            Button("backupSaveButton", BackupSave);
            Button("deleteSaveButton", DeleteSave);
            Refresh();
        }

        private void Button(string name, Action action) => rootVisualElement.Q<Button>(name).clicked += action;
        private static string SavePath => LocalSavePath.Current;

        private static T Resolve<T>() where T : class
        {
            if (!EditorApplication.isPlaying)
            {
                return null;
            }

            var active = SceneManager.GetActiveScene();
            var scopes = Object.FindObjectsByType<LifetimeScope>()
                .Where(s => s.gameObject.scene == active && s.Container != null);
            foreach (var scope in scopes)
            {
                if (scope.Container.TryResolve(typeof(T), out object result))
                {
                    return result as T;
                }
            }

            var root = Object.FindAnyObjectByType<RootLifetimeScope>();
            return root?.Container != null && root.Container.TryResolve(typeof(T), out object value) ? value as T : null;
        }

        private static Wall ActiveWall() => Object.FindObjectsByType<Wall>()
            .FirstOrDefault(w => w.gameObject.scene == SceneManager.GetActiveScene());

        private void Refresh()
        {
            if (rootVisualElement.Q<Label>("runtimeStatus") == null)
            {
                return;
            }

            var manager = Resolve<StageManager>();
            rootVisualElement.Q<Label>("runtimeStatus").text = EditorApplication.isPlaying
                ? $"{SceneManager.GetActiveScene().name} · {(manager == null ? "로비 / 씬 전환 중" : manager.State.ToString())}"
                : "게임을 재생하면 오류 재현 버튼이 활성화됩니다.";
            var wall = EditorApplication.isPlaying ? ActiveWall() : null;
            Enable("breakWallButton", manager?.State == StageState.Playing && wall != null && !wall.IsDestroyed);
            rootVisualElement.Q<Label>("wallStatus").text = wall == null ? "전투 중 방벽 HP가 표시됩니다." : $"방벽 {wall.CurrentHp} / {wall.MaxHp}";
            var fault = Resolve<ISubmitFaultSwitch>();
            Enable("failButton", fault != null);
            Enable("clearFaultButton", fault != null && fault.FailNextSubmits > 0);
            rootVisualElement.Q<Label>("faultStatus").text = fault == null ? "Local 백엔드로 재생할 때 사용할 수 있습니다." : $"남은 실패 횟수: {fault.FailNextSubmits}";
            rootVisualElement.Q<Label>("savePath").text = SavePath;
            Enable("backupSaveButton", !EditorApplication.isPlayingOrWillChangePlaymode && File.Exists(SavePath));
            Enable("deleteSaveButton", !EditorApplication.isPlayingOrWillChangePlaymode && File.Exists(SavePath));
        }

        private void Enable(string name, bool enabled) => rootVisualElement.Q<Button>(name).SetEnabled(enabled);

        private void BackupSave()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(SavePath))
            {
                return;
            }

            try
            {
                string backup = Backup(SavePath);
                rootVisualElement.Q<Label>("saveStatus").text = "백업: " + backup;
            }
            catch (Exception error) { rootVisualElement.Q<Label>("saveStatus").text = error.Message; Debug.LogException(error); }
        }

        // 지우기 전에 백업해 두어 실수로 지워도 .bak에서 되살릴 수 있다
        private void DeleteSave()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(SavePath))
            {
                return;
            }

            if (!EditorUtility.DisplayDialog("세이브 삭제", "save.json을 지웁니다. 지우기 전에 같은 폴더에 백업합니다.\n다음 재생은 새 세이브로 시작합니다.", "삭제", "취소"))
            {
                return;
            }

            try
            {
                string backup = Delete(SavePath);
                rootVisualElement.Q<Label>("saveStatus").text = "삭제함 (백업: " + backup + ")";
            }
            catch (Exception error) { rootVisualElement.Q<Label>("saveStatus").text = error.Message; Debug.LogException(error); }
        }

        internal static string Delete(string path)
        {
            string backup = Backup(path);
            File.Delete(path);
            return backup;
        }

        internal static string Backup(string path)
        {
            string backup = path + "." + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..8] + ".bak";
            File.Copy(path, backup, false);
            return backup;
        }
    }
}
