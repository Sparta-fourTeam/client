using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using Game.Boot;
using Game.Core;
using Game.Core.Defense;
using Game.Network;
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
            Button("restoreSaveButton", RestoreSave);
            Button("addGoldButton", () => Cheat((save, _) => PlaytestCheats.AddGold(save, Int("goldField"))));
            Button("fillEnergyButton", () => Cheat((save, data) => PlaytestCheats.FillEnergy(save, data.Energy)));
            Button("setLevelButton", () => Cheat((save, data) => PlaytestCheats.SetLevel(save, data.PlayerLevels, Int("levelField"), false)));
            Button("setLevelEdgeButton", () => Cheat((save, data) => PlaytestCheats.SetLevel(save, data.PlayerLevels, Int("levelField"), true)));
            Button("completeTutorialsButton", () => Cheat(PlaytestCheats.CompleteTutorials));
            Button("resetTutorialsButton", () => Cheat((save, _) => PlaytestCheats.ResetTutorials(save)));
            Button("unlockStagesButton", () => Cheat(PlaytestCheats.UnlockAllStages));
            Button("healWallButton", () => { var wall = ActiveWall(); if (wall != null) { wall.Repair(wall.MaxHp); } });
            Button("speed1Button", () => Resolve<StageManager>()?.SetPlaytestSpeed(1f));
            Button("speed4Button", () => Resolve<StageManager>()?.SetPlaytestSpeed(4f));
            Button("speed8Button", () => Resolve<StageManager>()?.SetPlaytestSpeed(8f));
            Refresh();
        }

        private int Int(string name) => rootVisualElement.Q<IntegerField>(name).value;

        // Local 세이브를 고쳐 저장하고 프로필에 즉시 반영한다
        private void Cheat(Action<LocalSave, GameDataStore> mutate)
        {
            var store = Resolve<LocalSaveStore>();
            var data = Resolve<GameDataStore>();
            var api = Resolve<IPlayerApi>();
            var profile = Resolve<PlayerProfile>();
            if (store == null || data == null || api == null || profile == null)
            {
                return;
            }

            try
            {
                var save = store.Load();
                mutate(save, data);
                store.Flush(save);
                api.GetMe().ContinueWith(snapshot => profile.Apply(snapshot)).Forget(Debug.LogException);
                rootVisualElement.Q<Label>("cheatStatus").text = "적용함 " + DateTime.Now.ToString("HH:mm:ss");
            }
            catch (Exception error) { rootVisualElement.Q<Label>("cheatStatus").text = error.Message; Debug.LogException(error); }
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
            bool local = Resolve<LocalSaveStore>() != null;
            foreach (string name in new[] { "addGoldButton", "fillEnergyButton", "setLevelButton", "setLevelEdgeButton", "completeTutorialsButton", "resetTutorialsButton", "unlockStagesButton" })
            {
                Enable(name, local);
            }

            Enable("healWallButton", manager?.State == StageState.Playing && wall != null && !wall.IsDestroyed);
            foreach (string name in new[] { "speed1Button", "speed4Button", "speed8Button" })
            {
                Enable(name, manager?.State == StageState.Playing);
            }

            Enable("restoreSaveButton", !EditorApplication.isPlayingOrWillChangePlaymode && LatestBackup(SavePath) != null);
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

        private void RestoreSave()
        {
            string backup = LatestBackup(SavePath);
            if (EditorApplication.isPlayingOrWillChangePlaymode || backup == null)
            {
                return;
            }

            if (!EditorUtility.DisplayDialog("세이브 복원", "현재 save.json을 백업한 뒤 아래 파일로 되돌립니다.\n" + Path.GetFileName(backup), "복원", "취소"))
            {
                return;
            }

            try
            {
                string saved = Restore(SavePath, backup);
                rootVisualElement.Q<Label>("saveStatus").text = "복원함 (이전 세이브 백업: " + (saved ?? "없음") + ")";
            }
            catch (Exception error) { rootVisualElement.Q<Label>("saveStatus").text = error.Message; Debug.LogException(error); }
        }

        /// <summary>가장 최근 .bak. 새 백업은 같은 시각에도 증가하는 UTC tick을 파일명에 사용한다</summary>
        internal static string LatestBackup(string path)
        {
            string directory = Path.GetDirectoryName(path);
            return Directory.Exists(directory)
                ? Directory.GetFiles(directory, Path.GetFileName(path) + ".*.bak").OrderBy(f => f, StringComparer.Ordinal).LastOrDefault()
                : null;
        }

        /// <summary>현재 세이브가 있으면 먼저 백업하고 backup 내용으로 되돌린다. 백업 파일은 남겨 둔다</summary>
        internal static string Restore(string path, string backup)
        {
            string saved = File.Exists(path) ? Backup(path) : null;
            File.Copy(backup, path, true);
            return saved;
        }

        internal static string Delete(string path)
        {
            string backup = Backup(path);
            File.Delete(path);
            return backup;
        }

        internal static string Backup(string path)
        {
            const string format = "yyyyMMdd-HHmmss.fffffff";
            var time = DateTime.UtcNow;
            string latest = LatestBackup(path);
            // 같은 tick, 시계 역행, 에디터 재로드 뒤에도 기존 백업보다 뒤에 정렬되게 한다
            if (latest != null && DateTime.TryParseExact(
                Path.GetFileName(latest)[(Path.GetFileName(path).Length + 1)..^4], format,
                CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var previous) && time <= previous)
            {
                time = previous.AddTicks(1);
            }
            string backup = path + "." + time.ToString(format, CultureInfo.InvariantCulture) + ".bak";
            File.Copy(path, backup, false);
            return backup;
        }
    }
}
