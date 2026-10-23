#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using Game.Boot;
using Game.Core;
using Game.Core.Defense;
using Game.Network;
using Game.View;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VContainer;
using Button = UnityEngine.UI.Button;
using Object = UnityEngine.Object;

namespace Game.Tests
{
    public sealed class UiRuntimeFlowTests : IPrebuildSetup, IPostBuildCleanup
    {
#if UNITY_EDITOR
        private const string EditorStateKey = "Game.Tests.UiRuntimeFlowTests.EditorState";
        private static string PlayFromBootKey => $"ProjectNova.PlayFromBoot.{Hash128.Compute(Application.dataPath)}";
#endif

        void IPrebuildSetup.Setup()
        {
#if UNITY_EDITOR
            // This runs before entering Play Mode, unlike NUnit/Unity setup callbacks.
            // SessionState survives the domain reload so cleanup can restore the developer's settings.
            SessionState.SetString(EditorStateKey + ".Scene", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
            SessionState.SetBool(EditorStateKey + ".HadPreference", EditorPrefs.HasKey(PlayFromBootKey));
            SessionState.SetBool(EditorStateKey + ".Preference", EditorPrefs.GetBool(PlayFromBootKey, false));
            SessionState.SetBool(EditorStateKey + ".HadServerAuth", EditorPrefs.HasKey(ServerConfig.EditorAuthPreferenceKey));
            SessionState.SetBool(EditorStateKey + ".ServerAuth", ServerConfig.UseServerAuth);
            EditorPrefs.SetBool(ServerConfig.EditorAuthPreferenceKey, false);
            SessionState.SetBool(EditorStateKey, true);
            // PlayFromBootMenu would otherwise restore Boot again after the domain reload.
            EditorPrefs.SetBool(PlayFromBootKey, false);
            EditorSceneManager.playModeStartScene = null;
#endif
        }

        void IPostBuildCleanup.Cleanup()
        {
#if UNITY_EDITOR
            if (!SessionState.GetBool(EditorStateKey, false)) { return; }
            string scenePath = SessionState.GetString(EditorStateKey + ".Scene", "");
            EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(scenePath) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
            if (SessionState.GetBool(EditorStateKey + ".HadPreference", false))
            {
                EditorPrefs.SetBool(PlayFromBootKey, SessionState.GetBool(EditorStateKey + ".Preference", false));
            }
            else { EditorPrefs.DeleteKey(PlayFromBootKey); }
            if (SessionState.GetBool(EditorStateKey + ".HadServerAuth", false))
            {
                EditorPrefs.SetBool(ServerConfig.EditorAuthPreferenceKey, SessionState.GetBool(EditorStateKey + ".ServerAuth", false));
            }
            else { EditorPrefs.DeleteKey(ServerConfig.EditorAuthPreferenceKey); }
            SessionState.EraseBool(EditorStateKey + ".HadServerAuth");
            SessionState.EraseBool(EditorStateKey + ".ServerAuth");
            SessionState.EraseString(EditorStateKey + ".Scene");
            SessionState.EraseBool(EditorStateKey + ".HadPreference");
            SessionState.EraseBool(EditorStateKey + ".Preference");
            SessionState.EraseBool(EditorStateKey);
#endif
        }

        private string _savePath;
        private string _originalSavePath;
        private LocalSaveStore _store;
        private bool _hadSpeedPreference;
        private float _speedPreference;
        private bool _restoreSpeedPreference;
        private bool _hadMissionSelection;
        private int _missionSelection;
        private IAuthCredentialStore _credentials;
        private bool _hadCredential;
        private LoginMethod _credentialMethod;
        private string _credentialAccountId;
        private bool _restoreConsentPreference, _hadConsentPreference;
        private bool _restoreLanguagePreference, _hadLanguagePreference;
        private int _languagePreference;
        private string _consentPreference;
        private ILoginConsentStore _consents;
        private static readonly FieldInfo StorePath = typeof(LocalSaveStore).GetField("_filePath", BindingFlags.Instance | BindingFlags.NonPublic);
        private static T Field<T>(object target, string name) =>
            (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

        [UnityTest]
        public IEnumerator BattleCheckpoint_LobbyPromptRestoresBattleAndCardSelectionWithoutChargingAgain()
        {
            yield return StartFromBoot(UnlockLobbyFeatures);
            var root = Object.FindAnyObjectByType<RootLifetimeScope>();
            var checkpoints = root.Container.Resolve<IBattleCheckpointStore>();
            var context = root.Container.Resolve<StageContext>();
            var nav = root.Container.Resolve<ISceneNavigator>();
            yield return Object.FindAnyObjectByType<LobbyLifetimeScope>().Container.Resolve<BattleLauncher>().Launch(1).ToCoroutine();
            yield return WaitFor(() => Object.FindAnyObjectByType<BattleCheckpointDriver>()?.Capture() != null);
            yield return WaitFor(() => Object.FindObjectsByType<Enemy>().Any(e => !e.IsDead));
            var stage = Object.FindAnyObjectByType<StageLifetimeScope>();
            var manager = stage.Container.Resolve<StageManager>();
            manager.Pause();
            var enemy = Object.FindObjectsByType<Enemy>().First(e => !e.IsDead);
            var skills = stage.Container.Resolve<SkillController>();
            foreach (var definition in stage.Container.Resolve<ISkillDataProvider>().LoadAll()
                .Where(d => !d.childOnly).GroupBy(d => d.castType).Select(g => g.First()))
            {
                if (!Field<List<SkillBase>>(skills, "skills").Any(w => w.Data.id == definition.id))
                { Assert.That(skills.AddWeapon(definition.id), Is.True); }
            }
            foreach (var skill in Field<List<SkillBase>>(skills, "skills").Cast<SkillCaster>())
            { skill.FireAt(enemy.Position); }
            enemy.ApplyFreeze(7.5f);
            enemy.ApplySlow(0.3f, 4.25f);
            var driver = Object.FindAnyObjectByType<BattleCheckpointDriver>();
            driver.SaveNow();
            var saved = checkpoints.Load();
            Assert.That(saved, Is.Not.Null);
            Assert.That(saved.Skills, Is.Not.Empty);
            Assert.That(saved.Skills.Sum(s => s.Attacks.Count), Is.GreaterThan(0), "진행 중인 공격도 복원 대상으로 검증한다");
            int energy = _store.Load().wallet.energyStored;
            int issued = _store.Load().battles.Count;
            string battleId = context.BattleId;

            // 새 프로세스에서는 Root의 전투 문맥이 비어 있다. 실제 로비·확인 버튼·Stage 로딩 경로를 거친다.
            context.SetBattle(null);
            yield return nav.GoToLobby().ToCoroutine();
            var lobbyUi = Object.FindAnyObjectByType<LobbyLifetimeScope>().Container.Resolve<IUiRegistry>();
            var confirm = lobbyUi.Get<ConfirmPopupView>();
            yield return WaitFor(() => confirm.IsOpen);
            yield return ClickGameButton(Field<Button>(confirm, "_confirmButton"));
            yield return WaitFor(() => Object.FindAnyObjectByType<BattleCheckpointDriver>()?.Capture() != null);
            stage = Object.FindAnyObjectByType<StageLifetimeScope>();
            manager = stage.Container.Resolve<StageManager>();
            var restored = Object.FindAnyObjectByType<BattleCheckpointDriver>().Capture();
            Assert.That(stage.Container.Resolve<GameDataStore>().BattleSourceRevision,
                Is.EqualTo(saved.Stage.ProgressSourceRevision));
            Assert.That(context.BattleId, Is.EqualTo(battleId));
            Assert.That(_store.Load().wallet.energyStored, Is.EqualTo(energy));
            Assert.That(_store.Load().battles.Count, Is.EqualTo(issued));
            Assert.That(manager.State, Is.EqualTo(StageState.Paused));
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(restored.Stage.Clock, Is.EqualTo(saved.Stage.Clock));
            Assert.That(restored.Stage.WallHp, Is.EqualTo(saved.Stage.WallHp));
            Assert.That(restored.Spawns.Enemies.Select(e => e.Id).ToArray(), Is.EqualTo(saved.Spawns.Enemies.Select(e => e.Id).ToArray()), $"dead={string.Join(",", restored.Stage.DeadEnemyIds)} effects={string.Join(",", restored.Skills.Select(w => w.SkillId + ":" + w.Attacks.Count))}");
            for (int i = 0; i < saved.Spawns.Enemies.Count; i++)
            {
                Assert.That(restored.Spawns.Enemies[i].Position, Is.EqualTo(saved.Spawns.Enemies[i].Position));
                Assert.That(restored.Spawns.Enemies[i].Hp, Is.EqualTo(saved.Spawns.Enemies[i].Hp));
                Assert.That(restored.Spawns.Enemies[i].Status.Freeze, Is.EqualTo(saved.Spawns.Enemies[i].Status.Freeze));
                Assert.That(restored.Spawns.Enemies[i].Status.Slow, Is.EqualTo(saved.Spawns.Enemies[i].Status.Slow));
            }
            Assert.That(restored.Skills.Select(s => s.Cooldown), Is.EqualTo(saved.Skills.Select(s => s.Cooldown)));
            Assert.That(restored.Skills.Select(s => s.Attacks.Count), Is.EqualTo(saved.Skills.Select(s => s.Attacks.Count)));
            manager.Resume();
            yield return CompleteEnemiesUntil(stage, () => manager.State == StageState.CardSelect);
            driver = Object.FindAnyObjectByType<BattleCheckpointDriver>();
            yield return WaitFor(() =>
            {
                var state = driver.Capture()?.Stage;
                return state != null && state.ProgressRevision > 0 && state.PendingProgress == null && !state.ProgressRequested;
            }, "진행 보고 응답을 체크포인트에 반영");
            driver.SaveNow();
            var selected = checkpoints.Load();
            Assert.That(selected.Stage.Choices, Is.Not.Empty);
            var accepted = _store.Load().battles.Single(b => b.battleKey == battleId);
            var receipt = accepted.progressReceipts.Single(r => r.ack.revision == selected.Stage.ProgressRevision);
            Assert.That(receipt.ack.revision, Is.EqualTo(receipt.request.expectedRevision + 1));
            Assert.That(receipt.ack.killedMonsters, Is.EqualTo(receipt.request.killedMonsters));
            Assert.That(accepted.reportedKills, Is.EqualTo(receipt.ack.killedMonsters));
            Assert.That(selected.Stage.Kills, Is.GreaterThanOrEqualTo(receipt.ack.killedMonsters),
                "보고 이후 사망 연출 대기 중 발생한 처치는 현재 체크포인트에 더 들어갈 수 있다");
            Assert.That(receipt.request.sourceRevision, Is.EqualTo(stage.Container.Resolve<GameDataStore>().BattleSourceRevision));
            Assert.That(selected.Stage.ProgressRevision, Is.EqualTo(accepted.progressRevision));
            Assert.That(selected.Stage.PendingProgress, Is.Null);
            context.SetBattle(null);
            yield return nav.GoToLobby().ToCoroutine();
            confirm = Object.FindAnyObjectByType<LobbyLifetimeScope>().Container.Resolve<IUiRegistry>().Get<ConfirmPopupView>();
            yield return WaitFor(() => confirm.IsOpen);
            yield return ClickGameButton(Field<Button>(confirm, "_confirmButton"));
            yield return WaitFor(() => Object.FindAnyObjectByType<BattleCheckpointDriver>()?.Capture() != null);
            stage = Object.FindAnyObjectByType<StageLifetimeScope>();
            manager = stage.Container.Resolve<StageManager>();
            Assert.That(manager.State, Is.EqualTo(StageState.CardSelect));
            restored = Object.FindAnyObjectByType<BattleCheckpointDriver>().Capture();
            Assert.That(restored.Stage.Choices.Select(c => (c.SkillId, c.CardId, c.NewWeapon, c.General)),
                Is.EqualTo(selected.Stage.Choices.Select(c => (c.SkillId, c.CardId, c.NewWeapon, c.General))));
            yield return PickCardsAndResume(stage);
            var fault = stage.Container.Resolve<ISubmitFaultSwitch>();
            fault.FailNextSubmits = StageManager.AutoRetryCount + 1;
            manager.RetryDelay = TimeSpan.Zero;
            stage.Container.Resolve<Wall>().TakeDamage(int.MaxValue);
            yield return WaitFor(() => Field<GameObject>(stage.Container.Resolve<IUiRegistry>().Get<SubmitResultPopupView>(), "_panel").activeSelf);
            Object.FindAnyObjectByType<BattleCheckpointDriver>().SaveNow();
            Assert.That(checkpoints.Load().Stage.State, Is.EqualTo(StageState.Submitting));
            context.SetBattle(null);
            fault.FailNextSubmits = 0;
            yield return nav.GoToLobby().ToCoroutine();
            yield return WaitFor(() => Object.FindAnyObjectByType<StageLifetimeScope>() != null
                && Object.FindAnyObjectByType<StageLifetimeScope>().Container.Resolve<StageManager>().State == StageState.Finished);
            Assert.That(context.BattleId, Is.EqualTo(battleId));
            Assert.That(checkpoints.Load(), Is.Null, "결과 반영 뒤에는 전투를 다시 복원하면 안 된다");
            Assert.That(_store.Load().battles.Count, Is.EqualTo(issued));
            LogAssert.NoUnexpectedReceived();
        }

        // 스테이지 클리어로 열리는 로비 기능(FeatureUnlocks)이 모두 열린 세이브로 만든다
        private static void UnlockLobbyFeatures(LocalSave save)
        {
            var data = VContainer.Unity.VContainerSettings.Instance.GetOrCreateRootLifetimeScopeInstance().Container.Resolve<GameDataStore>();
            foreach (var stage in data.Stages.Values)
            {
                var row = save.stageProgress.Find(p => p.stageId == stage.Id);
                if (row == null)
                {
                    row = new StageProgressRow { stageId = stage.Id };
                    save.stageProgress.Add(row);
                }

                row.clearRating = row.claimedRating = 1;
            }
        }

        [UnityTest]
        public IEnumerator BootLobbyStageRoundTripKeepsViewsAndSubscriptionsAlive()
        {
            yield return StartFromBoot();
            var root = Object.FindAnyObjectByType<RootLifetimeScope>();
            var navigator = root.Container.Resolve<ISceneNavigator>();

            for (int pass = 0; pass < 2; pass++)
            {
                var lobby = Object.FindAnyObjectByType<LobbyLifetimeScope>();
                var ui = lobby.Container.Resolve<IUiRegistry>();
                var chat = ui.Get<ChatView>();
                yield return ClickGameButton(Field<Button>(ui.Get<ChatOpenButton>(), "_button"));
                Assert.That(Field<GameObject>(chat, "_panel").activeSelf, Is.True);
                Assert.That(Field<Button>(chat, "_sendButton").interactable, Is.False, "로컬 인증에서는 실제 서버 채팅 전송을 막는다");
                var retryButton = Field<Button>(chat, "_retryButton");
                var window = Field<RectTransform>(chat, "_window");
                var retryBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(window, retryButton.transform);
                var sendBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(window, Field<Button>(chat, "_sendButton").transform);
                Assert.That(retryBounds.min.y, Is.GreaterThan(sendBounds.max.y), "재접속과 전송의 클릭 영역을 분리한다");
                yield return ClickGameButton(retryButton);
                yield return ClickGameButton(Field<Button>(chat, "_emojiButton"));
                Assert.That(Field<GameObject>(chat, "_emojiPanel").activeSelf, Is.True);
                retryBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(window, retryButton.transform);
                sendBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(window, Field<Button>(chat, "_sendButton").transform);
                var emojiBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(window, Field<GameObject>(chat, "_emojiPanel").transform);
                Assert.That(retryBounds.min.y, Is.GreaterThan(sendBounds.max.y));
                Assert.That(retryBounds.min.y, Is.GreaterThan(emojiBounds.max.y), "이모지 창이 재접속 버튼을 덮지 않는다");
                yield return ClickGameButton(retryButton);
                yield return ClickGameButton(Field<Button[]>(chat, "_emojiChoices")[0]);
                Assert.That(Field<TMP_InputField>(chat, "_input").text, Does.Contain(ChatEmojiPalette.Values[0]));
                yield return ClickGameButton(Field<Button>(chat, "_closeButton"));
                Assert.That(Field<GameObject>(chat, "_panel").activeSelf, Is.False);

                var mission = ui.Get<MissionScreenView>();
                var recovery = ui.Get<EnergyRecoverPopupView>();
                var recoveryPanel = Field<GameObject>(recovery, "_panel");
                var missionEnergy = mission.GetComponentInChildren<EnergyView>(true);
                var lobbyEnergy = Object.FindObjectsByType<EnergyView>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Single(view => view.gameObject.scene == lobby.gameObject.scene && !view.transform.IsChildOf(mission.transform));
                // 홈에서도 재화를 표시하며, 임무의 실제 에너지 아이콘으로 충전 창을 연다.
                Assert.That(lobbyEnergy.gameObject.activeInHierarchy, Is.True, "홈에서도 에너지 재화를 표시한다");
                yield return ClickGameButton(Field<Button>(ui.Get<MissionButtonView>(), "_button"));
                Assert.That(Field<GameObject>(mission, "_panel").activeSelf, Is.True);
                yield return ClickGameButton(Field<Button>(missionEnergy, "_button"), (RectTransform)missionEnergy.transform.Find("Icon"));
                Assert.That(recoveryPanel.activeSelf, Is.True);
                yield return ClickGameButton(Field<Button>(recovery, "_closeButton"));
                yield return WaitFor(() => !recoveryPanel.activeSelf);
                Assert.That(Field<GameObject>(mission, "_panel").activeSelf, Is.True, "충전 창을 닫아도 전투 준비 화면은 유지한다");
                yield return ClickGameButton(Field<Button>(mission, "_backButton"));
                yield return WaitFor(() => !Field<GameObject>(mission, "_panel").activeSelf);
                ui.Get<ProfilePopupView>().Open();
                Assert.That(Field<GameObject>(ui.Get<ProfilePopupView>(), "_panel").activeSelf, Is.True);
                if (pass == 0)
                {
                    var profile = root.Container.Resolve<PlayerProfile>();
                    string playerId = profile.PlayerId;
                    var portrait = ui.Get<ProfilePopupView>().GetComponentsInChildren<ProfilePortraitView>(true).Single();
                    yield return ClickGameButton(Field<Button>(portrait, "_button"));
                    var picker = ui.Get<ProfilePortraitPickerView>();
                    Assert.That(Field<GameObject>(picker, "_panel").activeSelf, Is.True);
                    int avatar = (profile.AvatarId + 1) % ProfilePortraits.Count;
                    yield return ClickGameButton(Field<Button[]>(picker, "_choices")[avatar]);
                    yield return WaitFor(() => profile.AvatarId == avatar && !Field<GameObject>(picker, "_panel").activeSelf);
                    Assert.That(root.Container.Resolve<IProfileApi>().GetProfile().GetAwaiter().GetResult().AvatarId, Is.EqualTo(avatar));
                    var confirm = ui.Get<ConfirmPopupView>();
                    yield return ClickGameButton(Field<Button>(ui.Get<ProfilePopupView>(), "_renameButton"));
                    yield return WaitFor(() => confirm.IsOpen);
                    Field<TMP_InputField>(confirm, "_input").text = "플레이검증_2";
                    yield return ClickGameButton(Field<Button>(confirm, "_confirmButton"));
                    yield return WaitFor(() => profile.Nickname == "플레이검증_2");
                    Assert.That(profile.PlayerId, Is.EqualTo(playerId));
                }
                ui.Get<CharacterScreenView>().SetVisible(true);
                ui.Get<SkillScreenView>().SetVisible(true);
                ui.Get<SkillUpgradePopupView>().Open(0);
                ui.Get<EquipUpgradePopupView>().Open(0);
                ui.Get<EnergyRecoverPopupView>().Open();
                ui.Get<MissionScreenView>().Open();
                yield return lobby.Container.Resolve<BattleLauncher>().Launch(1).ToCoroutine();
                yield return null;
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Stage"));
                var stage = Object.FindAnyObjectByType<StageLifetimeScope>();
                var stageUi = stage.Container.Resolve<IUiRegistry>();
                var manager = stage.Container.Resolve<StageManager>();
                yield return ClickGameButton(Field<Button>(stageUi.Get<StageControlView>(), "_pauseButton"));
                yield return WaitFor(() => manager.State == StageState.Paused);
                Assert.That(Field<GameObject>(stageUi.Get<PauseMenuView>(), "_panel").activeSelf, Is.True);
                yield return ClickGameButton(Field<Button>(stageUi.Get<PauseMenuView>(), "_resumeButton"));
                Assert.That(manager.State, Is.EqualTo(StageState.Playing));
                yield return ClickGameButton(Field<Button>(stageUi.Get<StageControlView>(), "_pauseButton"));
                var fault = root.Container.Resolve<ISubmitFaultSwitch>();
                if (pass == 0)
                {
                    fault.FailNextSubmits = StageManager.AutoRetryCount + 1;
                }
                else
                {
                    fault.FailNextSubmits = 0;
                }

                manager.RetryDelay = TimeSpan.Zero;
                yield return ClickGameButton(Field<Button>(stageUi.Get<PauseMenuView>(), "_abandonButton"));
                if (pass == 0)
                {
                    yield return WaitFor(() => Field<GameObject>(stageUi.Get<SubmitResultPopupView>(), "_panel").activeSelf);
                    fault.FailNextSubmits = 0;
                    yield return ClickGameButton(Field<Button>(stageUi.Get<SubmitResultPopupView>(), "_retryButton"));
                }
                yield return WaitFor(() => Field<GameObject>(stageUi.Get<ResultPopupView>(), "_panel").activeSelf);
                Field<Button>(stageUi.Get<ResultPopupView>(), "_lobbyButton").onClick.Invoke();
                yield return WaitFor(() => SceneManager.GetActiveScene().name == "Lobby" && navigator.Current == SceneId.Lobby);
                yield return null;
                Assert.That(Object.FindObjectsByType<UiHost>().Length, Is.EqualTo(1));
            }
        }

        [UnityTest]
        public IEnumerator Language_SelectingDisplayedFallbackPersistsExplicitChoice()
        {
            yield return StartFromBoot();
            var store = new LanguageSelectionStore();
            var settings = new GameSettings(store, GameLanguage.Japanese);
            var canvas = new GameObject("LanguageSelectionCanvas", typeof(RectTransform), typeof(Canvas));
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Lobby/SettingsPopup.prefab");
                var instance = Object.Instantiate(prefab, canvas.transform);
                var view = instance.GetComponent<SettingsPopupView>();
                view.Construct(Object.FindAnyObjectByType<RootLifetimeScope>().Container.Resolve<SoundSettings>(), settings);
                view.Initialize();
                view.Open();
                yield return null; // 활성화된 TMP 드롭다운의 Start가 실행된 뒤 실제 선택 목록을 연다.
                var dropdown = Field<TMP_Dropdown>(view, "_languageDropdown");
                dropdown.Show();
                yield return WaitFor(() => dropdown.GetComponentsInChildren<UnityEngine.UI.Toggle>().Any());
                Assert.IsFalse(store.Values.ContainsKey(GameSettings.LanguageKey), "선택지를 열기만 하면 기본값을 저장하지 않는다");
                var selected = dropdown.GetComponentsInChildren<UnityEngine.UI.Toggle>().Single(toggle => toggle.isOn);
                ExecuteEvents.Execute(selected.gameObject, new PointerEventData(EventSystem.current)
                { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
                yield return null;
                Assert.AreEqual((int)GameLanguage.Japanese, store.Values[GameSettings.LanguageKey]);
                Assert.AreEqual(GameLanguage.Japanese, new GameSettings(store, GameLanguage.English).Language,
                    "같은 항목을 명시적으로 누른 뒤에는 시스템 언어가 달라져도 선택을 유지한다");
            }
            finally { Object.Destroy(canvas); }
            yield return null;
        }

        private sealed class LanguageSelectionStore : ISettingsStore
        {
            public readonly Dictionary<string, int> Values = new();
            public bool TryGet(string key, out int value) => Values.TryGetValue(key, out value);
            public void Set(string key, int value) => Values[key] = value;
        }

        [UnityTest]
        public IEnumerator Language_ChangeUpdatesVisibleTextInPlaceWithoutReload()
        {
            // 언어를 바꿔도 씬을 다시 로드하지 않고, 화면에 보이는 문구가 모두 새 언어로 바뀐다(한글이 남지 않는다)
            yield return StartFromBoot();
            var settings = Object.FindAnyObjectByType<RootLifetimeScope>().Container.Resolve<GameSettings>();
            var host = Object.FindAnyObjectByType<UiHost>();
            var hangul = new System.Text.RegularExpressions.Regex("[\\uAC00-\\uD7A3]");

            System.Collections.Generic.List<UnityEngine.UI.Button> NavButtons()
            {
                var found = new System.Collections.Generic.List<UnityEngine.UI.Button>();
                foreach (var button in Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Exclude))
                {
                    // 잠긴 탭을 덮는 자물쇠 판은 누를 때마다 일시적인 토스트를 새로 띄우므로 제외한다
                    if (button.name.Contains("Lock")) { continue; }
                    for (var tr = button.transform; tr != null; tr = tr.parent)
                    {
                        if (tr.name == "LobbyBottomNav") { found.Add(button); break; }
                    }
                }
                return found;
            }

            string KoreanLeft()
            {
                var left = new System.Collections.Generic.List<string>();
                foreach (var label in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Exclude))
                {
                    // 언어 이름은 현재 언어와 무관하게 자기 글자로 보여야 한다
                    if (!label.gameObject.activeInHierarchy || label.text == "한국어" || !hangul.IsMatch(label.text)) { continue; }
                    left.Add(label.name + ": " + label.text);
                }
                return string.Join(", ", left);
            }

            // 한국어로 각 화면을 한 번씩 그려 두고(상시 떠 있는 라벨이 한국어로 채워진다) 영어로 바꾼다
            foreach (var button in NavButtons()) { button.onClick.Invoke(); for (int i = 0; i < 15; i++) { yield return null; } }
            settings.SetLanguage(GameLanguage.English);
            yield return WaitFor(() => SelectedLocaleCode() == "en");
            for (int i = 0; i < 30; i++) { yield return null; }
            Assert.That(Object.FindAnyObjectByType<UiHost>(), Is.SameAs(host), "씬을 다시 로드하지 않는다");
            Assert.That(KoreanLeft(), Is.Empty, "로비에 한글이 남았다");

            foreach (var button in NavButtons())
            {
                button.onClick.Invoke();
                for (int i = 0; i < 15; i++) { yield return null; }
                Assert.That(KoreanLeft(), Is.Empty, button.transform.name + " 화면에 한글이 남았다");
            }

            // 잠깐 떠서 위 측정에 잡히지 않는 기본 토스트도 새 언어로 나와야 한다
            var toast = Object.FindAnyObjectByType<ComingSoonToastView>(FindObjectsInactive.Include);
            Assert.That(toast, Is.Not.Null);
            toast.Show();
            for (int i = 0; i < 10; i++) { yield return null; }
            Assert.That(KoreanLeft(), Is.Empty, "토스트에 한글이 남았다");

            settings.SetLanguage(GameLanguage.Korean);
            yield return WaitFor(() => SelectedLocaleCode() == "ko");
        }

        [UnityTest]
        public IEnumerator Boot_InEnglishNeverShowsKoreanWhileCurtainIsOpen()
        {
            // 영어로 부팅했을 때 커튼이 화면을 가리지 않는 프레임마다 활성 라벨에 한글이 없다.
            // 주의: 에디터에서는 번들·테이블 로드가 빨라 Preload·동기 초기화를 빼도 통과한다(측정으로 확인). 실제 기기의 로드 대기 틈을 증명하지는 못하고,
            // 부팅 직후 한국어가 노출되는 다른 회귀(프리팹 기본 문구를 읽어 쓰는 코드, 키가 빠진 문구 등)를 막는다
            var hangul = new System.Text.RegularExpressions.Regex("[\\uAC00-\\uD7A3]");
            var frames = 0;
            void CheckFrame()
            {
                var curtain = Object.FindAnyObjectByType<TransitionCurtain>(FindObjectsInactive.Include);
                if (curtain != null && curtain.Coverage >= 0.5f) { return; }
                frames++;
                var left = new List<string>();
                foreach (var label in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Exclude))
                {
                    // 언어 이름은 현재 언어와 무관하게 자기 글자로 보여야 한다
                    if (!label.gameObject.activeInHierarchy || label.text == "한국어" || !hangul.IsMatch(label.text)) { continue; }
                    left.Add(label.name + ": " + label.text);
                }
                Assert.That(left, Is.Empty, $"커튼이 열린 프레임 {Time.frameCount}(씬 {SceneManager.GetActiveScene().name})에 한글이 보였다: " + string.Join(", ", left));
            }

            yield return StartFromBoot(language: GameLanguage.English, onFrame: CheckFrame, coldLocalization: true);
            Assert.That(frames, Is.GreaterThan(0), "검사가 한 프레임도 실행되지 않았다");
        }

        private static string SelectedLocaleCode()
            => UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale?.Identifier.Code;

        // seed가 없으면 튜토리얼을 마친 계정으로 시작한다. 튜토리얼 중인 새 계정은 로비 대신 1스테이지로 들어간다
        private IEnumerator StartFromBoot(Action<LocalSave> seed = null, string expectedScene = "Lobby", bool allowAnalytics = false,
            GameLanguage language = GameLanguage.Korean, Action onFrame = null, bool coldLocalization = false)
        {
            // 개발 기기에 저장된 언어나 시스템 언어가 화면 문구를 바꾸므로 언어(기본 한국어)를 명시해 고정한다
            // 한 테스트에서 다시 부팅해도 원래 값은 처음 한 번만 저장한다. 다시 읽으면 앞서 고정한 값이 원래 값으로 복원된다
            if (!_restoreLanguagePreference)
            {
                _hadLanguagePreference = PlayerPrefs.HasKey("settings.language");
                _languagePreference = PlayerPrefs.GetInt("settings.language");
            }
            PlayerPrefs.SetInt("settings.language", (int)language);
            _restoreLanguagePreference = true;
            if (coldLocalization)
            {
                // 앱을 처음 켠 상태를 재현한다: Localization 상태를 비우고 실제 부팅 훅을 부른 뒤, 로케일은 시작 선택기(저장된 언어)가 고르게 둔다
                UnityEngine.Localization.Settings.LocalizationSettings.Instance.ResetState();
                typeof(Game.Boot.BootFlow).Assembly.GetType("Game.Boot.LocalizationBoot")
                    .GetMethod("Initialize", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
            }
            else
            {
                // 도메인 리로드 없이 이어지는 재생이나 앞선 테스트가 남긴 로케일이 새지 않게 지정한 언어로 확정한다
                UnityEngine.Localization.Settings.LocalizationSettings.InitializationOperation.WaitForCompletion();
                UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale = UnityEngine.Localization.Settings.LocalizationSettings.AvailableLocales.GetLocale(GameLanguageLocale.Code(language));
            }
            _hadSpeedPreference = PlayerPrefs.HasKey("stage_speed");
            _speedPreference = PlayerPrefs.GetFloat("stage_speed");
            _restoreSpeedPreference = true;
            PlayerPrefs.DeleteKey("stage_speed");
            _hadMissionSelection = PlayerPrefs.HasKey("mission_selected_stage");
            _missionSelection = PlayerPrefs.GetInt("mission_selected_stage");
            PlayerPrefs.DeleteKey("mission_selected_stage");
            var root = VContainer.Unity.VContainerSettings.Instance.GetOrCreateRootLifetimeScopeInstance() as RootLifetimeScope;
            Assert.That(root, Is.Not.Null);
            // 루트가 저장값을 심기 전에 만들어졌을 수 있어 시스템 언어(기본값)가 새지 않게 지정한 언어를 직접 적용한다
            var languageSettings = root.Container.Resolve<GameSettings>();
            if (languageSettings.Language != language) { languageSettings.SetLanguage(language); }
            _store = root.Container.Resolve<LocalSaveStore>();
            _originalSavePath = (string)StorePath.GetValue(_store);
            _savePath = Path.Combine(Path.GetTempPath(), "nova-ui-" + Guid.NewGuid().ToString("N") + ".json");
            StorePath.SetValue(_store, _savePath);
            var save = _store.Load();
            var data = root.Container.Resolve<GameDataStore>();
            save.tutorials = data.Tutorials.Select(t => t.Id).ToList();
            save.wallet.energyStored = data.Energy.Max;
            save.wallet.energyUpdatedAt = DateTime.UtcNow.ToString("O");
            seed?.Invoke(save);
            _store.Flush(save);
            // 로그인 정보는 개발자 PC의 것을 보관해 두고 비운다. 그래서 매번 최초 진입처럼 로그인창이 뜬다 (Cleanup에서 복원)
            _credentials = root.Container.Resolve<IAuthCredentialStore>();
            _hadCredential = _credentials.HasCredential;
            _credentialMethod = _credentials.Method;
            _credentialAccountId = _credentials.AccountId;
            _credentials.Clear();
            _hadConsentPreference = PlayerPrefs.HasKey(PlayerPrefsLoginConsentStore.DefaultKey);
            _consentPreference = PlayerPrefs.GetString(PlayerPrefsLoginConsentStore.DefaultKey, "");
            _restoreConsentPreference = true;
            _consents = root.Container.Resolve<ILoginConsentStore>();
            _consents.Clear();
            var navigator = root.Container.Resolve<ISceneNavigator>();
            var boot = new BootFlow(navigator);
            SceneManager.LoadSceneAsync("Boot");
            yield return WaitFor(() => SceneManager.GetActiveScene().name == "Boot", onFrame: onFrame);
            boot.Start();
            yield return AcceptServiceConditions(allowAnalytics, onFrame);
            yield return WaitFor(() => SceneManager.GetActiveScene().name == "Loading" && LoginPanelShown(), onFrame: onFrame);
            var login = Object.FindAnyObjectByType<LoginView>();
            yield return ClickGameButton(Field<Button>(login, "_guestButton"));
            var confirm = Field<ConfirmPopupView>(login, "_confirm");
            yield return WaitFor(() => confirm.IsOpen, onFrame: onFrame);
            yield return ClickGameButton(Field<Button>(confirm, "_confirmButton"));
            yield return WaitFor(() => SceneManager.GetActiveScene().name == expectedScene && navigator.Current.ToString() == expectedScene, onFrame: onFrame);
            yield return null;
        }

        private static IEnumerator AcceptServiceConditions(bool allowAnalytics = false, Action onFrame = null)
        {
            yield return WaitFor(() => SceneManager.GetActiveScene().name == "Loading" && Object.FindAnyObjectByType<ServiceConsentView>() != null, onFrame: onFrame);
            var view = Object.FindAnyObjectByType<ServiceConsentView>();
            Assert.That(Field<UnityEngine.UI.Toggle>(view, "_analyticsToggle").isOn, Is.False);
            Assert.That(Field<UnityEngine.UI.Toggle>(view, "_transferToggle").isOn, Is.False);
            // 한국어로 부팅한 흐름에서만 문구를 단언한다(다른 언어로 부팅하는 테스트도 이 헬퍼를 쓴다)
            if (SelectedLocaleCode() == "ko") { Assert.That(Field<TMP_Text>(view, "_startLabel").text, Is.EqualTo("전체 동의")); }
            Assert.That(view.GetComponentsInChildren<TMP_Text>(true).Any(t => t.text.Contains("14")), Is.False);
            yield return ClickGameButton(Field<Button>(view, allowAnalytics ? "_startButton" : "_declineButton"));
            Assert.That(Field<UnityEngine.UI.Toggle>(view, "_analyticsToggle").isOn, Is.EqualTo(allowAnalytics));
            Assert.That(Field<UnityEngine.UI.Toggle>(view, "_transferToggle").isOn, Is.EqualTo(allowAnalytics));
            Assert.That(Field<GameObject>(view, "_termsPanel").activeSelf, Is.True);
            Assert.That(Field<UnityEngine.UI.Toggle>(view, "_termsToggle").isOn, Is.False);
            Assert.That(Field<Button>(view, "_startButton").interactable, Is.False,
                "약관의 명시적 체크 전에는 확인으로 로그인하지 않는다");
            yield return ClickGameTarget(Field<UnityEngine.UI.Toggle>(view, "_termsToggle"));
            yield return ClickGameButton(Field<Button>(view, "_startButton"));
        }

        private static bool LoginPanelShown()
        {
            var login = Object.FindAnyObjectByType<LoginView>();
            return login != null && Field<GameObject>(login, "_panel").activeSelf;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            // Unload the game before restoring the real save path or preferences.
            var empty = SceneManager.CreateScene("UiRuntimeTestEmpty");
            SceneManager.SetActiveScene(empty);
            foreach (var name in new[] { "Boot", "Loading", "Lobby", "Stage" })
            {
                var scene = SceneManager.GetSceneByName(name);
                if (scene.IsValid() && scene.isLoaded) { yield return SceneManager.UnloadSceneAsync(scene); }
            }
            var root = Object.FindAnyObjectByType<RootLifetimeScope>();
            if (root != null) { Object.Destroy(root.gameObject); yield return null; }
            if (_store != null && _originalSavePath != null) { StorePath.SetValue(_store, _originalSavePath); }
            if (_restoreSpeedPreference)
            {
                if (_hadSpeedPreference) { PlayerPrefs.SetFloat("stage_speed", _speedPreference); }
                else { PlayerPrefs.DeleteKey("stage_speed"); }
                _restoreSpeedPreference = false;
                if (_hadMissionSelection) { PlayerPrefs.SetInt("mission_selected_stage", _missionSelection); }
                else { PlayerPrefs.DeleteKey("mission_selected_stage"); }
            }
            if (_credentials != null)
            {
                if (_hadCredential) { _credentials.Save(_credentialMethod, _credentialAccountId); }
                else { _credentials.Clear(); }
                _credentials = null;
            }
            if (_savePath != null && File.Exists(_savePath)) { File.Delete(_savePath); }
            if (_savePath != null)
            {
                foreach (string suffix in new[] { ".battle", ".battle.bak", ".battle.tmp" })
                { if (File.Exists(_savePath + suffix)) { File.Delete(_savePath + suffix); } }
            }
            if (_restoreLanguagePreference)
            {
                if (_hadLanguagePreference) { PlayerPrefs.SetInt("settings.language", _languagePreference); }
                else { PlayerPrefs.DeleteKey("settings.language"); }
                PlayerPrefs.Save();
                _restoreLanguagePreference = false;
            }
            if (_restoreConsentPreference)
            {
                if (_hadConsentPreference) { PlayerPrefs.SetString(PlayerPrefsLoginConsentStore.DefaultKey, _consentPreference); }
                else { PlayerPrefs.DeleteKey(PlayerPrefsLoginConsentStore.DefaultKey); }
                PlayerPrefs.Save();
                _restoreConsentPreference = false;
            }
            _store = null;
            _originalSavePath = null;
            _savePath = null;
        }

        private static IEnumerator ClickGameButton(Button button, RectTransform hitArea = null)
            => ClickGameTarget(button, hitArea);

        private static IEnumerator ClickGameTarget(Component target, RectTransform hitArea = null, Vector2? localPoint = null)
        {
            // A focused tool/preview can temporarily supply Screen's editor dimensions.
            // Overlay raycasters must read the same Game View size as the canvas.
#if UNITY_EDITOR
            var gameView = Resources.FindObjectsOfTypeAll<EditorWindow>()
                .FirstOrDefault(window => window.GetType().FullName == "UnityEditor.GameView");
            gameView?.Focus();
#endif
            yield return null;
            yield return WaitFor(() => target != null && target.gameObject.activeInHierarchy
                && (target is not Button button || button.IsInteractable()), $"클릭 가능: {target?.name}");
            Canvas.ForceUpdateCanvases();
            var canvas = target.GetComponentInParent<Canvas>();
            var rect = hitArea != null ? hitArea : (RectTransform)target.transform;
            PointerEventData pointer = null;
            var hits = new List<RaycastResult>();
            bool sampled = false;
            void SampleGameView()
            {
                if (sampled)
                {
                    return;
                }

                if (canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                    && (Mathf.Abs(canvas.rootCanvas.pixelRect.width - Screen.width) >= 1
                        || Mathf.Abs(canvas.rootCanvas.pixelRect.height - Screen.height) >= 1))
                {
                    return;
                }

                pointer = new PointerEventData(EventSystem.current)
                {
                    position = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, rect.TransformPoint(localPoint ?? rect.rect.center)),
                    button = PointerEventData.InputButton.Left
                };
                hits.Clear();
                EventSystem.current.RaycastAll(pointer, hits);
                sampled = hits.Count > 0 && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) == target.gameObject;
            }
            // Editor update can retain Scene View/preview dimensions even with Game View focused.
            // Sample the real raycast while the Game View canvas is preparing its render.
            Canvas.willRenderCanvases += SampleGameView;
            try
            {
#if UNITY_EDITOR
                gameView?.Repaint();
#endif
                Canvas.ForceUpdateCanvases();
                float deadline = Time.realtimeSinceStartup + 20f;
                while (!sampled)
                {
                    Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline),
                        $"{target.name} did not become clickable. Top target={hits.FirstOrDefault().gameObject?.name}, Screen={Screen.width}x{Screen.height}");
                    yield return null;
                }
            }
            finally { Canvas.willRenderCanvases -= SampleGameView; }
            Assert.That(hits, Is.Not.Empty, $"{target.name} has no pointer target. Pointer={pointer.position}, Screen={Screen.width}x{Screen.height}, Camera={canvas.worldCamera?.pixelRect}, Canvas={canvas.pixelRect}, Rect={rect.rect}, Position={rect.position}, EventSystem={EventSystem.current.name}");
            Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject), Is.SameAs(target.gameObject), target.name + " is covered by " + hits[0].gameObject.name);
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
            yield return null;
        }

        private static IEnumerator CompleteEnemiesUntil(StageLifetimeScope stage, Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + 20;
            while (!condition())
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "Enemy progression did not reach card selection.");
                yield return AdvanceAndDefeatEnemies(stage);
            }
        }

        private static IEnumerator AdvanceAndDefeatEnemies(StageLifetimeScope stage)
        {
            // Expedite the real spawn/damage path; never publish synthetic WaveCompleted/StageEnded messages.
            var manager = stage.Container.Resolve<StageManager>();
            var rewards = stage.Container.Resolve<StageRewardTracker>();
            int completed = rewards.CompletedWaves;
            stage.Container.Resolve<EnemySpawner>().Advance(0.35f);
            foreach (var enemy in Object.FindObjectsByType<Enemy>().Where(e => e.gameObject.scene == stage.gameObject.scene && !e.IsDead).ToArray())
            {
                if (rewards.CompletedWaves != completed || manager.State != StageState.Playing) { break; }
                enemy.TakeDamage(1000000);
            }
            if (rewards.CompletedWaves != completed)
            {
                // 실제 사망 연출·보상 지연 동안 다음 웨이브까지 강제로 처치하지 않는다.
                yield return WaitFor(() => manager.State != StageState.Playing, "웨이브 완료 후 카드·룰렛·결과 표시");
            }
            else { yield return null; }
        }

        private static IEnumerator PickCardsAndResume(StageLifetimeScope stage)
        {
            var manager = stage.Container.Resolve<StageManager>();
            var stats = stage.Container.Resolve<BattleStats>();
            var slots = Field<CardSlotView[]>(stage.Container.Resolve<IUiRegistry>().Get<CardSelectView>(), "_slots");
            float deadline = Time.realtimeSinceStartup + 20;
            while (manager.State == StageState.CardSelect)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "대기 카드 보상을 모두 처리하지 못했다");
                var choices = manager.Choices;
                int priorCards = stats.BuildLog.Count;
                yield return ClickGameButton(Field<Button>(slots.First(slot => slot.gameObject.activeSelf), "_button"));
                yield return WaitFor(() => stats.BuildLog.Count > priorCards, "선택 카드 적용");
                Assert.That(stats.BuildLog.Count, Is.EqualTo(priorCards + 1), "한 번의 클릭은 카드 한 장만 적용한다");
                yield return WaitFor(() => manager.State != StageState.CardSelect || !ReferenceEquals(choices, manager.Choices),
                    "다음 카드 보상 또는 전투로 전환");
            }
            yield return WaitFor(() => manager.State == StageState.Playing, "대기 카드 처리 후 전투 재개");
        }

        private static IEnumerator WaitFor(Func<bool> condition, string step = null,
            [System.Runtime.CompilerServices.CallerMemberName] string caller = null,
            [System.Runtime.CompilerServices.CallerLineNumber] int line = 0, Action onFrame = null)
        {
            float deadline = Time.realtimeSinceStartup + 20f;
            while (!condition())
            {
                onFrame?.Invoke();
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), $"UI flow did not complete in 20 seconds. Step={step ?? caller}, Line={line}, Scene={SceneManager.GetActiveScene().name}, Playing={Application.isPlaying}");
                yield return null;
            }
        }
    }
}
