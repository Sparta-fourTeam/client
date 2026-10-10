using System;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
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
        private bool _launching;
        private string _cardSignature;
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
            minSize = new Vector2(560, 580);
            rootVisualElement.Clear();
            _cardSignature = null;
            AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Layout).CloneTree(rootVisualElement);
            Button("launchButton", () => Launch().Forget(Debug.LogException));
            Button("pauseButton", () => Resolve<StageManager>()?.Pause());
            Button("resumeButton", () => Resolve<StageManager>()?.Resume());
            Button("forfeitButton", () => Resolve<StageManager>()?.Forfeit());
            Button("speedOneButton", () => Resolve<StageManager>()?.SetSpeed(1));
            Button("speedTwoButton", () => Resolve<StageManager>()?.SetSpeed(2));
            Button("breakWallButton", () => { var wall = ActiveWall(); if (wall != null) { wall.TakeDamage(wall.CurrentHp); } });
            Button("failButton", () => { var fault = Resolve<ISubmitFaultSwitch>(); if (fault != null) { fault.FailNextSubmits = StageManager.AutoRetryCount + 1; } });
            Button("clearFaultButton", () => { var fault = Resolve<ISubmitFaultSwitch>(); if (fault != null) { fault.FailNextSubmits = 0; } });
            Button("retryButton", () => Resolve<StageManager>()?.RetrySubmit());
            Button("saveFolderButton", () => EditorUtility.RevealInFinder(File.Exists(SavePath) ? SavePath : Application.persistentDataPath));
            Button("resetSaveButton", ResetSave);
            Refresh();
        }

        private void Button(string name, Action action) => rootVisualElement.Q<Button>(name).clicked += action;
        private static string SavePath => Path.Combine(Application.persistentDataPath, "save.json");

        internal static T Resolve<T>() where T : class
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

        private async UniTask Launch()
        {
            var launcher = Resolve<BattleLauncher>();
            var field = rootVisualElement.Q<DropdownField>("stageField");
            if (_launching || launcher == null || !int.TryParse(field.value, out int id))
            {
                return;
            }

            _launching = true;
            rootVisualElement.Q<Label>("launchStatus").text = "전투 입장 중…";
            Refresh();
            try
            {
                await launcher.Launch(id);
                if (this != null)
                {
                    rootVisualElement.Q<Label>("launchStatus").text = SceneManager.GetActiveScene().name == "Stage"
                    ? "전투에 입장했습니다." : "입장하지 못했습니다. 에너지와 해금 상태를 확인하세요.";
                }
            }
            catch (Exception error)
            {
                if (this != null)
                {
                    rootVisualElement.Q<Label>("launchStatus").text = "입장 실패: " + error.Message;
                }

                Debug.LogException(error);
            }
            finally
            {
                _launching = false; if (this != null)
                {
                    Refresh();
                }
            }
        }

        private void Refresh()
        {
            if (rootVisualElement.Q<Label>("runtimeStatus") == null)
            {
                return;
            }

            bool playing = EditorApplication.isPlaying;
            var manager = Resolve<StageManager>();
            var data = Resolve<GameDataStore>();
            var stages = rootVisualElement.Q<DropdownField>("stageField");
            var ids = data?.Stages.Values.OrderBy(s => s.Id).Select(s => s.Id.ToString()).ToList();
            if (ids != null && !ids.SequenceEqual(stages.choices))
            {
                string previous = stages.value;
                stages.choices = ids;
                stages.SetValueWithoutNotify(ids.Contains(previous) ? previous : ids.FirstOrDefault());
            }
            stages.SetEnabled(playing && Resolve<BattleLauncher>() != null && !_launching);
            rootVisualElement.Q<Label>("runtimeStatus").text = playing
                ? $"{SceneManager.GetActiveScene().name} · {(manager == null ? "로비 / 씬 전환 중" : manager.State.ToString())} · {Time.timeScale:0.#}배속"
                : "Boot부터 재생을 켜고 게임을 실행하세요. 재생 중 조작 버튼이 활성화됩니다.";
            Enable("launchButton", playing && Resolve<BattleLauncher>() != null && !_launching);
            Enable("pauseButton", manager?.State is StageState.Playing or StageState.CardSelect);
            Enable("resumeButton", manager?.State == StageState.Paused);
            Enable("forfeitButton", manager?.State == StageState.Paused);
            Enable("speedOneButton", manager?.State == StageState.Playing);
            Enable("speedTwoButton", manager?.State == StageState.Playing);
            var wall = playing ? ActiveWall() : null;
            Enable("breakWallButton", manager?.State == StageState.Playing && wall != null && !wall.IsDestroyed);
            rootVisualElement.Q<Label>("wallStatus").text = wall == null ? "전투 중 방벽 HP가 표시됩니다." : $"방벽 {wall.CurrentHp} / {wall.MaxHp}";
            var fault = Resolve<ISubmitFaultSwitch>();
            Enable("failButton", fault != null);
            Enable("clearFaultButton", fault != null && fault.FailNextSubmits > 0);
            Enable("retryButton", manager?.State == StageState.Submitting);
            rootVisualElement.Q<Label>("faultStatus").text = fault == null ? "Local 백엔드로 재생할 때 사용할 수 있습니다." : $"남은 실패 횟수: {fault.FailNextSubmits}";
            rootVisualElement.Q<Label>("savePath").text = SavePath;
            Enable("resetSaveButton", !EditorApplication.isPlayingOrWillChangePlaymode && File.Exists(SavePath));
            RefreshCards(manager);
        }

        private void Enable(string name, bool enabled) => rootVisualElement.Q<Button>(name).SetEnabled(enabled);

        private void RefreshCards(StageManager manager)
        {
            var choices = manager?.State == StageState.CardSelect ? manager.Choices : null;
            string signature = choices == null ? "" : string.Join("|", choices.Select(c => c.DisplayName + c.DisplayDescription));
            if (signature == _cardSignature)
            {
                return;
            }

            _cardSignature = signature;
            var container = rootVisualElement.Q<VisualElement>("cardChoices");
            container.Clear();
            if (choices == null)
            {
                return;
            }

            for (int i = 0; i < choices.Count; i++)
            {
                int index = i;
                container.Add(new Button(() => Resolve<StageManager>()?.PickCard(index))
                { text = choices[i].DisplayName + " · " + choices[i].DisplayDescription });
            }
        }

        private void ResetSave()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(SavePath))
            {
                return;
            }

            if (!EditorUtility.DisplayDialog("로컬 저장 초기화", "현재 save.json을 백업한 뒤 초기화합니다. 다음 재생부터 초기 데이터로 시작합니다.", "백업 후 초기화", "취소"))
            {
                return;
            }

            try
            {
                string backup = BackupAndReset(SavePath);
                rootVisualElement.Q<Label>("saveStatus").text = "백업: " + backup;
                Refresh();
            }
            catch (Exception error) { rootVisualElement.Q<Label>("saveStatus").text = error.Message; Debug.LogException(error); }
        }

        internal static string BackupAndReset(string path)
        {
            string backup = path + "." + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..8] + ".bak";
            File.Copy(path, backup, false);
            File.Delete(path);
            return backup;
        }
    }
}
