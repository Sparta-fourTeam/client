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
using Game.Editor;
using Game.Network;
using Game.View;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using VContainer;
using Button = UnityEngine.UI.Button;
using Object = UnityEngine.Object;

namespace Game.Tests
{
    public sealed class UiRuntimeFlowTests
    {
        private string _savePath;
        private string _originalSavePath;
        private LocalSaveStore _store;
        private PlaytestWindow _tool;
        private bool _hadSpeedPreference;
        private float _speedPreference;
        private bool _restoreSpeedPreference;
        private SceneAsset _originalPlayModeStartScene;
        private bool _restorePlayModeStartScene;
        private static readonly FieldInfo StorePath = typeof(LocalSaveStore).GetField("_filePath", BindingFlags.Instance | BindingFlags.NonPublic);
        private static T Field<T>(object target, string name) =>
            (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

        [UnityTest]
        public IEnumerator UpgradeButtonsRequireConfirmationAndEquipmentBatchSpendsExactTotals()
        {
            PrepareEditor();
            yield return new EnterPlayMode();
            yield return StartFromBoot();
            var lobby = Object.FindAnyObjectByType<LobbyLifetimeScope>();
            var data = lobby.Container.Resolve<GameDataStore>();
            var profile = lobby.Container.Resolve<PlayerProfile>();
            var ui = lobby.Container.Resolve<IUiRegistry>();
            var catalog = lobby.Container.Resolve<IGrowthCatalog>();
            void Seed(string id, int purchases)
            {
                var def = data.Upgrades.GetOrThrow(id);
                var save = _store.Load(); save.exp = 0;
                for (int i = 1; i < data.UnlockLevelOf(def); i++)
                {
                    save.exp += data.PlayerLevels.RequiredToNext(i);
                }

                save.wallet.gold = Enumerable.Range(1, purchases).Sum(def.CostAt);
                save.upgrades = new();
                save.items = new() { new() { itemId = def.MaterialItemId, quantity = Enumerable.Range(1, purchases).Sum(def.MaterialAt) } };
                _store.Flush(save); profile.Apply(new LocalPlayerApi(_store).GetMe().GetAwaiter().GetResult());
            }
            Seed("equipment.hat", 2);
            yield return ClickGameButton(Field<Button>(ui.Get<LobbyNavView>(), "_characterTab"));
            var equip = ui.Get<EquipUpgradePopupView>();
            equip.Open(0);
            var quote = UpgradePurchasePlan.Create(profile, data, "equipment.hat", true);
            int coins = profile.Gold, material = profile.ItemQuantity(quote.MaterialItemId);
            yield return ClickGameButton(Field<Button>(equip, "_upgradeAllButton"));
            var confirm = ui.Get<UpgradeConfirmPopupView>();
            Assert.That(Field<GameObject>(confirm, "_panel").activeSelf, Is.True);
            Assert.That(Field<UpgradePurchasePlan>(confirm, "_plan").Count, Is.EqualTo(2));
            Assert.That(profile.UpgradeLevel("equipment.hat"), Is.Zero);
            yield return ClickGameButton(Field<Button>(confirm, "_cancelButton"));
            yield return WaitFor(() => !Field<GameObject>(confirm, "_panel").activeSelf);
            Assert.That(profile.Gold, Is.EqualTo(coins));
            yield return ClickGameButton(Field<Button>(equip, "_upgradeAllButton"));
            yield return ClickGameButton(Field<Button>(confirm, "_confirmButton"));
            yield return WaitFor(() => !Field<GameObject>(confirm, "_panel").activeSelf);
            Assert.That(profile.UpgradeLevel("equipment.hat"), Is.EqualTo(2));
            Assert.That(profile.Gold, Is.EqualTo(coins - quote.CoinCost));
            Assert.That(profile.ItemQuantity(quote.MaterialItemId), Is.EqualTo(material - quote.MaterialCost));
            yield return ClickGameButton(Field<Button>(equip, "_closeButton"));
            yield return WaitFor(() => !Field<GameObject>(equip, "_panel").activeSelf);
            var firstSkill = catalog.Skills.First(info => info.UpgradeId != null);
            Seed(firstSkill.UpgradeId, 2);
            yield return ClickGameButton(Field<Button>(ui.Get<LobbyNavView>(), "_skillTab"));
            var skill = ui.Get<SkillUpgradePopupView>();
            int index = catalog.Skills.ToList().FindIndex(info => info.UpgradeId == firstSkill.UpgradeId);
            skill.Open(index);
            yield return ClickGameButton(Field<Button>(skill, "_upgradeButton"));
            Assert.That(profile.UpgradeLevel(firstSkill.UpgradeId), Is.Zero);
            yield return ClickGameButton(Field<Button>(confirm, "_confirmButton"));
            yield return WaitFor(() => !Field<GameObject>(confirm, "_panel").activeSelf);
            Assert.That(profile.UpgradeLevel(firstSkill.UpgradeId), Is.EqualTo(1));
            Assert.That(Field<TMP_Text>(skill, "_levelText").text, Does.Contain("1"));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator NotificationBadgesFollowClosedScreensAndProfileChangesWithoutBlockingButtons()
        {
            PrepareEditor();
            yield return new EnterPlayMode();
            yield return StartFromBoot();
            var lobby = Object.FindAnyObjectByType<LobbyLifetimeScope>();
            var profile = lobby.Container.Resolve<PlayerProfile>();
            var catalog = lobby.Container.Resolve<IGrowthCatalog>();
            var ui = lobby.Container.Resolve<IUiRegistry>();
            var nav = ui.Get<LobbyNavView>();
            var character = ui.Get<CharacterScreenView>();
            var skill = ui.Get<SkillScreenView>();
            NotificationBadgeView Badge(Component parent) => parent.GetComponentInChildren<NotificationBadgeView>(true);
            var characterTab = Field<Button>(nav, "_characterTab");
            var skillTab = Field<Button>(nav, "_skillTab");
            var homeTab = Field<Button>(nav, "_homeTab");
            PlayerSnapshot Snapshot(int gold, int claimed, List<ItemAmount> items = null) => new()
            {
                gold = gold,
                items = items ?? new(),
                stageProgress = new() {
                    new() { stageId = 1, clearRating = 3, claimedRating = claimed },
                    new() { stageId = 2 }
                }
            };
            profile.Apply(Snapshot(0, 2));
            Assert.That(Badge(characterTab).IsVisible, Is.False);
            Assert.That(Badge(skillTab).IsVisible, Is.False);
            Assert.That(Badge(homeTab).IsVisible, Is.True);
            Assert.That(Badge(ui.Get<MissionButtonView>()).IsVisible, Is.True);
            Assert.That(Field<GameObject>(character, "_panel").activeSelf, Is.False);
            Assert.That(Field<GameObject>(skill, "_panel").activeSelf, Is.False);
            var materials = catalog.Equips.Select(e => e.MaterialItemId).Concat(catalog.Skills.Select(e => e.MaterialItemId))
                .Where(id => id != null).Distinct().Select(id => new ItemAmount { itemId = id, quantity = 1000000 }).ToList();
            profile.Apply(Snapshot(1000000, 2, materials));
            Assert.That(Badge(characterTab).IsVisible, Is.True);
            Assert.That(Badge(skillTab).IsVisible, Is.True);
            var equips = Field<EquipSlotView[]>(character, "_equipSlots");
            for (int i = 0; i < equips.Length; i++)
            {
                Assert.That(Badge(equips[i]).IsVisible, Is.EqualTo(i < catalog.Equips.Count && LobbyAvailability.CanUpgrade(profile, catalog.Equips[i])));
            }

            var skills = Field<SkillListSlotView[]>(skill, "_slots");
            for (int i = 0; i < skills.Length; i++)
            {
                Assert.That(Badge(skills[i]).IsVisible, Is.EqualTo(i < catalog.Skills.Count && LobbyAvailability.CanUpgrade(profile, catalog.Skills[i])));
            }

            Assert.That(character.GetComponentsInChildren<Transform>(true).Single(t => t.name == "CharacterTab")
                .GetComponentInChildren<NotificationBadgeView>(true).IsVisible, Is.True);
            Assert.That(skill.GetComponentsInChildren<Transform>(true).Single(t => t.name == "SkillTab")
                .GetComponentInChildren<NotificationBadgeView>(true).IsVisible, Is.True);
            yield return ClickGameButton(characterTab);
            Assert.That(Field<GameObject>(character, "_panel").activeSelf, Is.True);
            yield return ClickGameButton(skillTab);
            Assert.That(Field<GameObject>(skill, "_panel").activeSelf, Is.True);
            yield return ClickGameButton(homeTab);
            profile.Apply(Snapshot(0, 3));
            Assert.That(Badge(characterTab).IsVisible, Is.False);
            Assert.That(Badge(skillTab).IsVisible, Is.False);
            Assert.That(Badge(homeTab).IsVisible, Is.False);
            Assert.That(Badge(ui.Get<MissionButtonView>()).IsVisible, Is.False);
            Assert.That(equips.All(slot => !Badge(slot).IsVisible), Is.True);
            Assert.That(skills.All(slot => !Badge(slot).IsVisible), Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator BootLobbyStageRoundTripKeepsViewsAndSubscriptionsAlive()
        {
            PrepareEditor();
            yield return new EnterPlayMode();
            yield return StartFromBoot();
            var root = Object.FindAnyObjectByType<RootLifetimeScope>();
            var navigator = root.Container.Resolve<ISceneNavigator>();

            for (int pass = 0; pass < 2; pass++)
            {
                var lobby = Object.FindAnyObjectByType<LobbyLifetimeScope>();
                var ui = lobby.Container.Resolve<IUiRegistry>();
                Assert.That(PlaytestWindow.Resolve<IUiRegistry>(), Is.SameAs(ui));
                ui.Get<ProfilePopupView>().Open();
                Assert.That(Field<GameObject>(ui.Get<ProfilePopupView>(), "_panel").activeSelf, Is.True);
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
                Assert.That(PlaytestWindow.Resolve<StageManager>(), Is.SameAs(manager));
                yield return ClickTool("pauseButton");
                yield return WaitFor(() => manager.State == StageState.Paused);
                Assert.That(Field<GameObject>(stageUi.Get<PauseMenuView>(), "_panel").activeSelf, Is.True);
                yield return ClickTool("resumeButton");
                Assert.That(manager.State, Is.EqualTo(StageState.Playing));
                yield return ClickTool("pauseButton");
                var fault = root.Container.Resolve<ISubmitFaultSwitch>();
                if (pass == 0)
                {
                    yield return ClickTool("failButton");
                }
                else
                {
                    fault.FailNextSubmits = 0;
                }

                manager.RetryDelay = TimeSpan.Zero;
                yield return ClickTool("forfeitButton");
                if (pass == 0)
                {
                    yield return WaitFor(() => Field<GameObject>(stageUi.Get<SubmitResultPopupView>(), "_panel").activeSelf);
                    fault.FailNextSubmits = 0;
                    yield return ClickTool("retryButton");
                }
                yield return WaitFor(() => Field<GameObject>(stageUi.Get<ResultPopupView>(), "_panel").activeSelf);
                Field<Button>(stageUi.Get<ResultPopupView>(), "_lobbyButton").onClick.Invoke();
                yield return WaitFor(() => SceneManager.GetActiveScene().name == "Lobby" && navigator.Current == SceneId.Lobby);
                yield return null;
                Assert.That(Object.FindObjectsByType<UiHost>().Length, Is.EqualTo(1));
            }
        }

        [UnityTest]
        public IEnumerator GameButtonsNavigatePickCardsAndShowDefeat()
        {
            PrepareEditor();
            yield return new EnterPlayMode();
            yield return StartFromBoot();
            var lobby = Object.FindAnyObjectByType<LobbyLifetimeScope>();
            var ui = lobby.Container.Resolve<IUiRegistry>();
            var nav = ui.Get<LobbyNavView>();
            yield return ClickGameButton(Field<Button>(nav, "_characterTab"));
            Assert.That(Field<GameObject>(ui.Get<CharacterScreenView>(), "_panel").activeSelf, Is.True);
            Assert.That(Field<GameObject>(ui.Get<SkillScreenView>(), "_panel").activeSelf, Is.False);
            yield return ClickGameButton(Field<Button>(nav, "_skillTab"));
            Assert.That(Field<GameObject>(ui.Get<CharacterScreenView>(), "_panel").activeSelf, Is.False);
            Assert.That(Field<GameObject>(ui.Get<SkillScreenView>(), "_panel").activeSelf, Is.True);
            yield return ClickGameButton(Field<Button>(nav, "_homeTab"));
            Assert.That(Field<GameObject>(ui.Get<SkillScreenView>(), "_panel").activeSelf, Is.False);
            yield return ClickGameButton(Field<Button>(ui.Get<MissionButtonView>(), "_button"));
            var mission = ui.Get<MissionScreenView>();
            Assert.That(Field<GameObject>(mission, "_panel").activeSelf, Is.True);
            yield return ClickGameButton(Field<Button>(mission, "_infoButton"));
            var details = Field<MissionPopupView>(mission, "_detailsPopup");
            Assert.That(Field<GameObject>(details, "_panel").activeSelf, Is.True);
            yield return ClickGameButton(Field<Button>(details, "_closeButton"));
            yield return WaitFor(() => !Field<GameObject>(details, "_panel").activeSelf);
            yield return ClickGameButton(Field<Button>(mission, "_enterButton"));
            yield return WaitFor(() => SceneManager.GetActiveScene().name == "Stage");
            yield return null;
            var stage = Object.FindAnyObjectByType<StageLifetimeScope>();
            var stageUi = stage.Container.Resolve<IUiRegistry>();
            var manager = stage.Container.Resolve<StageManager>();
            var stats = stage.Container.Resolve<BattleStats>();
            var controls = stageUi.Get<StageControlView>();
            yield return ClickGameButton(Field<Button>(controls, "_speedButton"));
            Assert.That(Time.timeScale, Is.EqualTo(2));
            yield return ClickGameButton(Field<Button>(controls, "_pauseButton"));
            Assert.That(manager.State, Is.EqualTo(StageState.Paused));
            Assert.That(Time.timeScale, Is.Zero);
            yield return ClickGameButton(Field<Button>(stageUi.Get<PauseMenuView>(), "_resumeButton"));
            Assert.That(manager.State, Is.EqualTo(StageState.Playing));
            Assert.That(Time.timeScale, Is.EqualTo(2));
            yield return CompleteEnemiesUntil(stage, () => manager.State == StageState.CardSelect);
            var cards = stageUi.Get<CardSelectView>();
            Assert.That(Field<GameObject>(cards, "_panel").activeSelf, Is.True);
            Assert.That(Time.timeScale, Is.Zero);
            var slots = Field<CardSlotView[]>(cards, "_slots");
            Assert.That(slots.Count(s => s.gameObject.activeSelf), Is.EqualTo(manager.Choices.Count));
            int priorCards = stats.BuildLog.Count;
            yield return ClickGameButton(Field<Button>(slots.First(s => s.gameObject.activeSelf), "_button"));
            yield return WaitFor(() => manager.State == StageState.Playing);
            Assert.That(stats.BuildLog.Count, Is.EqualTo(priorCards + 1));
            Assert.That(Time.timeScale, Is.EqualTo(2));
            var wall = stage.Container.Resolve<Wall>();
            wall.TakeDamage(10);
            Assert.That(Field<TMP_Text>(stageUi.Get<WallHpHudView>(), "_hpText").text, Is.EqualTo($"{wall.CurrentHp} / {wall.MaxHp}"));
            yield return ClickTool("breakWallButton");
            yield return WaitFor(() => manager.State == StageState.Finished);
            Assert.That(wall.IsDestroyed, Is.True);
            var result = stageUi.Get<ResultPopupView>();
            Assert.That(Field<TMP_Text>(result, "_titleText").text, Is.EqualTo("Fail !"));
            var tip = Field<ResultTipView>(result, "_tip");
            Assert.That(tip.IsShown, Is.True);
            ExecuteEvents.Execute(tip.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);
            Assert.That(tip.IsShown, Is.False);
            yield return ClickGameButton(Field<Button>(result, "_lobbyButton"));
            yield return WaitFor(() => SceneManager.GetActiveScene().name == "Lobby");
            yield return null;
            Assert.That(Time.timeScale, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<UiHost>().Length, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator AllAuthoredWavesClearAndPersistProgress()
        {
            PrepareEditor();
            yield return new EnterPlayMode();
            yield return StartFromBoot();
            yield return ClickTool("launchButton");
            yield return WaitFor(() => SceneManager.GetActiveScene().name == "Stage");
            yield return null;
            var stage = Object.FindAnyObjectByType<StageLifetimeScope>();
            var manager = stage.Container.Resolve<StageManager>();
            var stats = stage.Container.Resolve<BattleStats>();
            var ui = stage.Container.Resolve<IUiRegistry>();
            float deadline = Time.realtimeSinceStartup + 180;
            while (manager.State is StageState.Playing or StageState.CardSelect)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "Authored waves did not finish.");
                if (manager.State == StageState.CardSelect)
                {
                    yield return WaitFor(() => _tool.rootVisualElement.Q<VisualElement>("cardChoices").childCount == manager.Choices.Count);
                    var choice = _tool.rootVisualElement.Q<VisualElement>("cardChoices").Children().OfType<UnityEngine.UIElements.Button>().First();
                    yield return ClickToolButton(choice);
                }
                else
                {
                    AdvanceAndDefeatEnemies(stage);
                    yield return null;
                }
            }
            yield return WaitFor(() => manager.State == StageState.Finished);
            var result = ui.Get<ResultPopupView>();
            Assert.That(Field<TMP_Text>(result, "_titleText").text, Is.EqualTo("Clear !"));
            Assert.That(stats.ReachedWave, Is.EqualTo(stage.Container.Resolve<StageDefinition>().Waves.Count));
            Assert.That(stats.BuildLog.Count, Is.GreaterThan(0));
            Assert.That(Field<ResultTipView>(result, "_tip").IsShown, Is.False);
            yield return ClickGameButton(Field<Button>(result, "_lobbyButton"));
            yield return WaitFor(() => SceneManager.GetActiveScene().name == "Lobby");
            yield return null;
            var profile = PlaytestWindow.Resolve<PlayerProfile>();
            Assert.That(profile.IsStageCleared(1), Is.True);
            Assert.That(profile.HighestUnlockedStage, Is.GreaterThanOrEqualTo(2));
            Assert.That(File.Exists(_savePath), Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<UiHost>().Length, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator StoppingDuringSceneTransitionDoesNotTouchDestroyedObjects()
        {
            PrepareEditor();
            yield return new EnterPlayMode();
            yield return StartFromBoot();
            var curtain = Object.FindAnyObjectByType<TransitionCurtain>(FindObjectsInactive.Include);
            yield return WaitFor(() => !curtain.gameObject.activeSelf);
            PlaytestWindow.Resolve<ISceneNavigator>().GoToLobby().Forget(Debug.LogException);
            Assert.That(curtain.gameObject.activeSelf, Is.True);
            yield return null;
            yield return new ExitPlayMode();
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        private void PrepareEditor()
        {
            _originalPlayModeStartScene = EditorSceneManager.playModeStartScene;
            _restorePlayModeStartScene = true;
            EditorSceneManager.playModeStartScene = null;
            _tool = ScriptableObject.CreateInstance<PlaytestWindow>();
            _tool.Show();
            // Start in an empty scene so boot cannot read/write the real save before isolation is installed.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        private IEnumerator StartFromBoot()
        {
            _hadSpeedPreference = PlayerPrefs.HasKey("stage_speed");
            _speedPreference = PlayerPrefs.GetFloat("stage_speed");
            _restoreSpeedPreference = true;
            var root = Object.FindAnyObjectByType<RootLifetimeScope>();
            Assert.That(root, Is.Not.Null);
            _store = root.Container.Resolve<LocalSaveStore>();
            _originalSavePath = (string)StorePath.GetValue(_store);
            _savePath = Path.Combine(Path.GetTempPath(), "nova-ui-" + Guid.NewGuid().ToString("N") + ".json");
            StorePath.SetValue(_store, _savePath);
            var navigator = root.Container.Resolve<ISceneNavigator>();
            var boot = new BootFlow(navigator);
            SceneManager.LoadSceneAsync("Boot");
            yield return WaitFor(() => SceneManager.GetActiveScene().name == "Boot");
            boot.Start();
            yield return WaitFor(() => SceneManager.GetActiveScene().name == "Lobby" && navigator.Current == SceneId.Lobby);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (_store != null && _originalSavePath != null)
            {
                StorePath.SetValue(_store, _originalSavePath);
            }

            if (_restoreSpeedPreference)
            {
                if (_hadSpeedPreference)
                {
                    PlayerPrefs.SetFloat("stage_speed", _speedPreference);
                }
                else
                {
                    PlayerPrefs.DeleteKey("stage_speed");
                }

                _restoreSpeedPreference = false;
            }
            if (Application.isPlaying)
            {
                yield return new ExitPlayMode();
            }

            if (_restorePlayModeStartScene)
            {
                EditorSceneManager.playModeStartScene = _originalPlayModeStartScene;
                _restorePlayModeStartScene = false;
            }
            if (_tool != null)
            {
                Object.DestroyImmediate(_tool);
            }

            if (_savePath != null && File.Exists(_savePath))
            {
                File.Delete(_savePath);
            }
        }

        private IEnumerator ClickTool(string name)
        {
            var button = _tool.rootVisualElement.Q<UnityEngine.UIElements.Button>(name);
            yield return WaitFor(() => button.enabledSelf);
            yield return ClickToolButton(button);
        }

        private IEnumerator ClickToolButton(UnityEngine.UIElements.Button button)
        {
            _tool.Focus();
            button.Focus();
            Assert.That(button.enabledInHierarchy, Is.True, button.name);
            using (var e = NavigationSubmitEvent.GetPooled())
            {
                button.SendEvent(e);
            }

            yield return null;
        }

        private static IEnumerator ClickGameButton(Button button)
        {
            // A focused tool/preview can temporarily supply Screen's editor dimensions.
            // Overlay raycasters must read the same Game View size as the canvas.
            var gameView = Resources.FindObjectsOfTypeAll<EditorWindow>()
                .FirstOrDefault(window => window.GetType().FullName == "UnityEditor.GameView");
            gameView?.Focus();
            yield return null;
            yield return WaitFor(() => button != null && button.gameObject.activeInHierarchy && button.IsInteractable());
            yield return new WaitForSecondsRealtime(0.35f);
            Canvas.ForceUpdateCanvases();
            var canvas = button.GetComponentInParent<Canvas>();
            var rect = (RectTransform)button.transform;
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
                    position = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, rect.TransformPoint(rect.rect.center)),
                    button = PointerEventData.InputButton.Left
                };
                EventSystem.current.RaycastAll(pointer, hits);
                sampled = true;
            }
            // Editor update can retain Scene View/preview dimensions even with Game View focused.
            // Sample the real raycast while the Game View canvas is preparing its render.
            Canvas.willRenderCanvases += SampleGameView;
            try
            {
                gameView?.Repaint();
                Canvas.ForceUpdateCanvases();
                yield return WaitFor(() => sampled);
            }
            finally { Canvas.willRenderCanvases -= SampleGameView; }
            Assert.That(hits, Is.Not.Empty, $"{button.name} has no pointer target. Pointer={pointer.position}, Screen={Screen.width}x{Screen.height}, Camera={canvas.worldCamera?.pixelRect}, Canvas={canvas.pixelRect}, Rect={rect.rect}, Position={rect.position}, EventSystem={EventSystem.current.name}");
            Assert.That(hits[0].gameObject.GetComponentInParent<Button>(), Is.SameAs(button), button.name + " is covered by " + hits[0].gameObject.name);
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
                AdvanceAndDefeatEnemies(stage);
                yield return null;
            }
        }

        private static void AdvanceAndDefeatEnemies(StageLifetimeScope stage)
        {
            // Expedite the real spawn/damage path; never publish synthetic WaveCompleted/StageEnded messages.
            stage.Container.Resolve<EnemySpawner>().Advance(0.35f);
            foreach (var enemy in Object.FindObjectsByType<Enemy>().Where(e => e.gameObject.scene == stage.gameObject.scene && !e.IsDead).ToArray())
            {
                enemy.TakeDamage(1000000);
            }
        }

        private static IEnumerator WaitFor(Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + 20f;
            while (!condition())
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), $"UI flow did not complete in 20 seconds. Scene={SceneManager.GetActiveScene().name}, Playing={Application.isPlaying}");
                yield return null;
            }
        }
    }
}
