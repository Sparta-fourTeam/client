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
        private IAuthCredentialStore _credentials;
        private bool _hadCredential;
        private LoginMethod _credentialMethod;
        private string _credentialAccountId;
        private static readonly FieldInfo StorePath = typeof(LocalSaveStore).GetField("_filePath", BindingFlags.Instance | BindingFlags.NonPublic);
        private static T Field<T>(object target, string name) =>
            (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

        [UnityTest]
        public IEnumerator RewardPopup_LargeClaimsFitAndRepeatedOpenRestoresSmallClaims()
        {
            yield return StartFromBoot();
            var ui = Object.FindAnyObjectByType<LobbyLifetimeScope>().Container.Resolve<IUiRegistry>();
            var popup = ui.Get<RewardPopupView>();
            var list = Field<ResultRewardListView>(popup, "_rewards");
            var area = (RectTransform)list.transform;
            var slots = (RectTransform)list.Cells[0].transform.parent;
            var panel = Field<GameObject>(popup, "_panel");
            var tap = Field<Button>(popup, "_tapArea");
            var corners = new Vector3[4];

            foreach (int count in new[] { 9, 40, 3, 8, 0, 12, 3 })
            {
                var rewards = Enumerable.Range(0, count).Select(i => new ResultRewardItem(null, i + 1)).ToArray();
                popup.Show(rewards);
                yield return WaitFor(() => Field<PopupTransition>(popup, "_transition").Progress >= 0.99f);
                if (popup.IsPlaying) { yield return ClickGameButton(tap); }
                yield return WaitFor(() => !popup.IsPlaying);
                Canvas.ForceUpdateCanvases();
                Assert.That(panel.activeSelf, Is.True, "스킵 탭은 팝업을 닫으면 안 된다");
                Assert.That(list.Cells.Count(cell => cell.gameObject.activeSelf), Is.EqualTo(count));
                foreach (var cell in list.Cells.Where(cell => cell.gameObject.activeSelf))
                {
                    ((RectTransform)cell.transform).GetWorldCorners(corners);
                    foreach (var corner in corners)
                    {
                        var point = area.InverseTransformPoint(corner);
                        Assert.That(point.x, Is.InRange(area.rect.xMin - 0.1f, area.rect.xMax + 0.1f));
                        Assert.That(point.y, Is.InRange(area.rect.yMin - 0.1f, area.rect.yMax + 0.1f));
                    }
                }
                if (count <= 8) { Assert.That(slots.localScale, Is.EqualTo(Vector3.one)); }
                if (count == 9 || count == 40 || count == 3)
                {
                    string capture = Path.GetFullPath(Path.Combine(Application.dataPath, $"../Temp/reward-popup-{count}.png"));
                    if (File.Exists(capture)) { File.Delete(capture); }
                    ScreenCapture.CaptureScreenshot(capture);
                    yield return WaitFor(() => File.Exists(capture));
                }
                yield return ClickGameButton(tap);
                yield return WaitFor(() => !panel.activeSelf);
            }

            // 닫는 전환 중에 다시 열어도 이전 큰 목록의 축소와 트윈이 남지 않는다.
            popup.Show(Enumerable.Range(0, 40).Select(i => new ResultRewardItem(null, i + 1)).ToArray());
            popup.Close();
            popup.Show(new[] { new ResultRewardItem(null, 1) });
            yield return WaitFor(() => !popup.IsPlaying);
            Assert.That(panel.activeSelf, Is.True);
            Assert.That(slots.localScale, Is.EqualTo(Vector3.one));
            Assert.That(list.Cells.Count(cell => cell.gameObject.activeSelf), Is.EqualTo(1));
            yield return ClickGameButton(tap);
            yield return WaitFor(() => !panel.activeSelf);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator UpgradeButtonsRequireConfirmationAndEquipmentBatchSpendsExactTotals()
        {
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
        public IEnumerator Tutorial_NewAccountSeesIntroDialogueInLobbyThenStartsTutorialBattle()
        {
            // 첫 시작: 로비에서 도입 대사가 먼저 나오고, 건너뛰면 전투 튜토리얼 스테이지로 간다.
            // 지면 평소 결과창으로 로비에 나가고, 로비는 튜토리얼 전투로 다시 보내지 않는다
            yield return StartFromBoot(save => save.tutorials = new List<string>());
            var dialogue = Object.FindAnyObjectByType<DialogueView>();
            yield return WaitFor(() => dialogue.IsOpen);
            yield return ClickGameButton(Field<Button>(dialogue, "_skipButton"));
            yield return WaitFor(() => SceneManager.GetActiveScene().name == "Stage");
            var root = Object.FindAnyObjectByType<RootLifetimeScope>().Container;
            int tutorialStage = root.Resolve<GameDataStore>().Tutorials.First(t => t.IsBattle).StageId;
            Assert.That(root.Resolve<StageContext>().StageId, Is.EqualTo(tutorialStage));
            Assert.That(SceneManager.GetSceneByName("Lobby").isLoaded, Is.False);

            yield return null;
            var stage = Object.FindAnyObjectByType<StageLifetimeScope>();
            var wall = stage.Container.Resolve<Wall>();
            wall.TakeDamage(wall.CurrentHp);
            yield return WaitFor(() => stage.Container.Resolve<StageManager>().State == StageState.Finished);
            var result = stage.Container.Resolve<IUiRegistry>().Get<ResultPopupView>();
            Field<ResultTipView>(result, "_tip").OnPointerClick(new PointerEventData(EventSystem.current));
            yield return ClickGameButton(Field<Button>(result, "_lobbyButton"));
            yield return WaitFor(() => SceneManager.GetActiveScene().name == "Lobby");
            var ui = Object.FindAnyObjectByType<LobbyLifetimeScope>().Container.Resolve<IUiRegistry>();
            yield return WaitFor(() => Field<bool>(ui.Get<TutorialGuideView>(), "_ready"));
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Lobby"));
            Assert.That(ui.Get<DialogueView>().IsOpen, Is.False);
            Assert.That(root.Resolve<PlayerProfile>().IsTutorialDone(root.Resolve<GameDataStore>().Tutorials.First(t => t.IsBattle).Id), Is.False);
        }

        [UnityTest]
        public IEnumerator Tutorial_AfterBattleTutorialClear_ShowsLevelUpThenGuidesOnlyThroughUpgrade()
        {
            // 전투 튜토리얼 스테이지를 깬 직후: 튜토리얼 완료는 아직이다. 경험치가 모자라도 정한 레벨을 보장한다.
            // 가진 코인이 없어도 튜토리얼 보상 코인으로 안내한 첫 강화를 할 수 있어야 한다
            var root = VContainer.Unity.VContainerSettings.Instance.GetOrCreateRootLifetimeScopeInstance() as RootLifetimeScope;
            var data = root.Container.Resolve<GameDataStore>();
            var intro = data.Tutorials.First(t => t.IsBattle);
            var guideTutorial = data.Tutorials.First(t => t.IsGuide && t.After == intro.Id);
            string upgradeId = guideTutorial.Complete.UpgradeId;
            var upgrade = data.Upgrades.GetOrThrow(upgradeId);
            string weapon = data.LoadSkills().First(skill => skill.progressionId == upgradeId).name;
            yield return StartFromBoot(save =>
            {
                save.tutorials = new List<string>();
                save.stageProgress.Find(p => p.stageId == intro.StageId).clearRating = 1;
                save.wallet.gold = 0;
            });
            var lobby = Object.FindAnyObjectByType<LobbyLifetimeScope>();
            var profile = lobby.Container.Resolve<PlayerProfile>();
            var ui = lobby.Container.Resolve<IUiRegistry>();
            var levelUp = ui.Get<LevelUpPopupView>();
            yield return WaitFor(() => levelUp.IsOpen);
            // 해금 카드와 보상 카드만 센다 (레벨은 큰 숫자로 보여 준다). 풀에 남은 숨은 카드는 제외
            var cards = levelUp.GetComponentsInChildren<LevelUpLineView>().Where(line => line.gameObject.activeSelf)
                .Select(line => line.GetComponentInChildren<TMP_Text>().text).ToList();
            Assert.That(cards, Has.Count.EqualTo(3));
            Assert.That(cards[0], Does.Contain(weapon));
            Assert.That(cards[1], Does.Contain($"×{upgrade.CostAt(1)}"));
            Assert.That(cards[2], Does.Contain($"×{upgrade.MaterialAt(1)}"));
            Assert.That(profile.Level, Is.EqualTo(intro.Rewards.Single(r => r.Kind == TutorialReward.SetLevel).Level));

            var tapArea = Field<Button>(levelUp, "_confirmButton");
            if (levelUp.IsPlaying)
            {
                yield return ClickGameButton(tapArea);   // 첫 탭은 연출만 건너뛴다
            }

            yield return ClickGameButton(tapArea);
            // 레벨업 다음에 새 컨텐츠(스킬 탭) 해금 연출이 나온다. 탭으로 건너뛰고 닫는다
            var contentUnlock = ui.Get<ContentUnlockView>();
            yield return WaitFor(() => contentUnlock.IsOpen);
            var contentTap = Field<Button>(contentUnlock, "_tapArea");
            if (contentUnlock.IsPlaying)
            {
                yield return ClickGameButton(contentTap);
            }

            yield return ClickGameButton(contentTap);
            // 팝업 뒤 가이드 시작 전에 대사 장면이 먼저 나온다. SKIP으로 건너뛴다
            var dialogue = ui.Get<DialogueView>();
            yield return WaitFor(() => dialogue.IsOpen);
            yield return ClickGameButton(Field<Button>(dialogue, "_skipButton"));
            yield return WaitFor(() => !dialogue.IsOpen);
            var guide = ui.Get<TutorialGuideView>();
            var nav = ui.Get<LobbyNavView>();
            yield return WaitFor(() => Field<GameObject>(guide, "_root").activeSelf);
            // 강조한 버튼 외에는 가림판이 터치를 막는다
            Assert.That(TopHit(Field<Button>(nav, "_shopTab")), Is.EqualTo("Blocker"));
            yield return ClickGameButton(Field<Button>(nav, "_skillTab"));
            var slot = ui.Get<SkillScreenView>().SlotOf(upgradeId).GetComponent<SkillListSlotView>();
            yield return ClickGameButton(Field<Button>(slot, "_button"));
            yield return ClickGameButton(Field<Button>(ui.Get<SkillUpgradePopupView>(), "_upgradeButton"));
            yield return ClickGameButton(Field<Button>(ui.Get<UpgradeConfirmPopupView>(), "_confirmButton"));
            yield return WaitFor(() => profile.IsTutorialDone(guideTutorial.Id));
            Assert.That(profile.UpgradeLevel(upgradeId), Is.EqualTo(1));
            Assert.That(Field<GameObject>(guide, "_root").activeSelf, Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        // button 가운데를 실제로 눌렀을 때 가장 위에 맞는 오브젝트 이름
        private static string TopHit(Button button)
        {
            var rect = (RectTransform)button.transform;
            var canvas = button.GetComponentInParent<Canvas>();
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
                    rect.TransformPoint(rect.rect.center))
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            return hits.FirstOrDefault().gameObject?.name;
        }

        [UnityTest]
        public IEnumerator EliteDeath_RouletteAnimatesDuringPause_AppliesRewardsAndRestoresSpeed()
        {
            yield return EliteRouletteFlow(skip: false);
        }

        [UnityTest]
        public IEnumerator EliteDeath_RouletteSkipPreservesRewards_AndProcessesQueuedElite()
        {
            yield return EliteRouletteFlow(skip: true);
        }

        private IEnumerator EliteRouletteFlow(bool skip)
        {
            yield return StartFromBoot();
            var rouletteStage = Object.FindAnyObjectByType<RootLifetimeScope>().Container.Resolve<GameDataStore>().StageOrFirst(1);
            int originalMinimum = rouletteStage.EliteRouletteMinCards;
            // 검증용 입력에서 선언된 상한 횟수를 사용해 여러 회차와 보상 누적을 끝까지 확인한다.
            rouletteStage.EliteRouletteMinCards = rouletteStage.EliteRouletteMaxCards;
            try
            {
                var lobby = Object.FindAnyObjectByType<LobbyLifetimeScope>();
                yield return lobby.Container.Resolve<BattleLauncher>().Launch(1).ToCoroutine();
                yield return WaitFor(() => Object.FindAnyObjectByType<StageLifetimeScope>()?.Container != null);
                var stage = Object.FindAnyObjectByType<StageLifetimeScope>();
                var manager = stage.Container.Resolve<StageManager>();
                var ui = stage.Container.Resolve<IUiRegistry>();
                yield return WaitFor(() => manager.State == StageState.Playing && ui.Get<EliteRouletteView>() != null);
                var view = ui.Get<EliteRouletteView>();
                var stats = stage.Container.Resolve<BattleStats>();
                var data = stage.Container.Resolve<GameDataStore>();
                var elite = data.Monsters.Values.First(monster => monster.GetEnemyType() == EnemyType.Elite);
                // 실제 factory/model의 사망을 쓴다. 웨이브 밖에서 생성하므로 게이지 집계 대상에서 제외한다.
                var enemy = stage.Container.Resolve<IEnemyFactory>().Create(elite.Id, new Vector2(99, 99), isSummoned: true);
                manager.SetSpeed(2f);
                int priorCards = stats.BuildLog.Count;
                enemy.TakeDamage(enemy.Model.Hp);
                Assert.That(manager.State, Is.EqualTo(StageState.EliteRoulette));
                Assert.That(Time.timeScale, Is.Zero);
                var panel = Field<GameObject>(view, "_panel");
                Assert.That(panel.activeSelf, Is.True);
                int count = manager.Roulette.CardCount;
                var definition = stage.Container.Resolve<StageDefinition>();
                Assert.That(count, Is.InRange(definition.EliteRouletteMinCards, definition.EliteRouletteMaxCards));
                var spinner = Field<RectTransform>(view, "_spinner");
                float initial = spinner.localEulerAngles.z;
                yield return WaitFor(() => Mathf.Abs(Mathf.DeltaAngle(initial, spinner.localEulerAngles.z)) > 5f);
                Assert.That(Time.timeScale, Is.Zero, "회전은 unscaled time, 전투는 정지 상태여야 한다");
                var cards = Field<CardSlotView[]>(view, "_rewardCards");
                int totalRewards = count;
                if (skip)
                {
                    var skipButton = Field<Button>(view, "_skipButton");
                    var pending = manager.Roulette.PendingRound;
                    Assert.That(pending, Is.Not.Null);
                    // 두 번째 엘리트 보상은 첫 번째 스킵으로 함께 소비되면 안 된다.
                    var queuedEnemy = stage.Container.Resolve<IEnemyFactory>().Create(elite.Id, new Vector2(99, 99), isSummoned: true);
                    queuedEnemy.TakeDamage(queuedEnemy.Model.Hp);
                    string preview = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/elite-roulette-skip-button.png"));
                    if (File.Exists(preview)) { File.Delete(preview); }
                    ScreenCapture.CaptureScreenshot(preview);
                    yield return WaitFor(() => File.Exists(preview));
                    yield return ClickGameButton(skipButton);
                    Assert.That(manager.Roulette.IsComplete, Is.True, "스킵 입력에서 남은 회차를 모두 적용해야 한다");
                    Assert.That(stats.BuildLog.Count - priorCards, Is.EqualTo(count));
                    Assert.That(stats.BuildLog[priorCards], Is.EqualTo(pending.Winner.Option.id), "진행 중인 당첨 무기를 다시 뽑지 않는다");
                    Assert.That(cards.Count(card => card.gameObject.activeSelf), Is.EqualTo(count));
                    Assert.That(skipButton.interactable, Is.False);
                    skipButton.onClick.Invoke();
                    Assert.That(stats.BuildLog.Count - priorCards, Is.EqualTo(count), "반복 입력으로 중복 지급하지 않는다");
                    Assert.That(Time.timeScale, Is.Zero);
                    yield return WaitFor(() => skipButton.interactable && skipButton.GetComponentInChildren<TMP_Text>(true).text == "확인");
                    Assert.That(manager.State, Is.EqualTo(StageState.EliteRoulette), "결과 확인 전에는 다음 룰렛으로 넘어가지 않는다");
                    Assert.That(panel.activeSelf, Is.True);
                    yield return ClickGameButton(skipButton);
                    yield return WaitFor(() => !manager.Roulette.IsComplete && skipButton.interactable);
                    Assert.That(manager.State, Is.EqualTo(StageState.EliteRoulette));
                    Assert.That(Time.timeScale, Is.Zero, "대기 중인 다음 룰렛까지 전투는 정지 상태를 유지한다");
                    Assert.That(stats.BuildLog.Count - priorCards, Is.EqualTo(count));
                    count = manager.Roulette.CardCount;
                    totalRewards += count;
                    yield return ClickGameButton(skipButton);
                    Assert.That(manager.Roulette.IsComplete, Is.True);
                }
                yield return WaitFor(() => manager.Roulette.IsComplete
                    && Field<TMP_Text>(view, "_heading").text == "강화 완료");
                var confirmButton = Field<Button>(view, "_skipButton");
                yield return WaitFor(() => confirmButton.interactable
                    && confirmButton.GetComponentInChildren<TMP_Text>(true).text == "확인");
                string screenshot = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/elite-roulette-live.png"));
                if (File.Exists(screenshot)) { File.Delete(screenshot); }
                ScreenCapture.CaptureScreenshot(screenshot);
                yield return WaitFor(() => File.Exists(screenshot));
                Assert.That(panel.activeSelf, Is.True, "스킵 여부와 관계없이 전체 카드 결과를 확인할 때까지 유지한다");
                Assert.That(manager.State, Is.EqualTo(StageState.EliteRoulette));
                Assert.That(Time.timeScale, Is.Zero);
                Assert.That(cards.Count(card => card.gameObject.activeSelf), Is.EqualTo(count));
                yield return ClickGameButton(confirmButton);
                yield return WaitFor(() => manager.State == StageState.Playing);
                Assert.That(panel.activeSelf, Is.False);
                Assert.That(stats.BuildLog.Count - priorCards, Is.EqualTo(totalRewards));
                Assert.That(cards.Count(card => card.gameObject.activeSelf), Is.EqualTo(count));
                Assert.That(Time.timeScale, Is.EqualTo(2f));
                LogAssert.NoUnexpectedReceived();
            }
            finally { rouletteStage.EliteRouletteMinCards = originalMinimum; }
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
                var mission = ui.Get<MissionScreenView>();
                var recovery = ui.Get<EnergyRecoverPopupView>();
                var recoveryPanel = Field<GameObject>(recovery, "_panel");
                var missionEnergy = mission.GetComponentInChildren<EnergyView>(true);
                var lobbyEnergy = Object.FindObjectsByType<EnergyView>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Single(view => view.gameObject.scene == lobby.gameObject.scene && !view.transform.IsChildOf(mission.transform));
                // 두 화면의 실제 하트 이미지에 터치해 동일한 충전 팝업이 열리는지 확인한다.
                yield return ClickGameButton(Field<Button>(lobbyEnergy, "_button"), (RectTransform)lobbyEnergy.transform.Find("Icon"));
                Assert.That(recoveryPanel.activeSelf, Is.True);
                yield return ClickGameButton(Field<Button>(recovery, "_closeButton"));
                yield return WaitFor(() => !recoveryPanel.activeSelf);
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
        public IEnumerator GameButtonsNavigatePickCardsAndShowDefeat()
        {
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
            yield return ClickGameButton(Field<Button>(nav, "_inventoryTab"));
            Assert.That(Field<GameObject>(ui.Get<InventoryScreenView>(), "_panel").activeSelf, Is.True);
            Assert.That(Field<GameObject>(ui.Get<SkillScreenView>(), "_panel").activeSelf, Is.False);
            yield return ClickGameButton(Field<Button>(nav, "_shopTab"));
            Assert.That(Field<GameObject>(ui.Get<ShopScreenView>(), "_panel").activeSelf, Is.True);
            Assert.That(Field<GameObject>(ui.Get<InventoryScreenView>(), "_panel").activeSelf, Is.False);
            yield return ClickGameButton(Field<Button>(nav, "_homeTab"));
            Assert.That(Field<GameObject>(ui.Get<ShopScreenView>(), "_panel").activeSelf, Is.False);
            yield return ClickGameButton(ui.Get<LobbyMenuOpenButton>().GetComponent<Button>());
            Assert.That(ui.Get<LobbyMenuPopupView>().IsOpen, Is.True);
            var menu = ui.Get<LobbyMenuPopupView>();
            yield return ClickGameButton(Field<Button>(menu, "_closeButton"));
            Assert.That(menu.IsOpen, Is.False);
            yield return WaitFor(() => !Field<GameObject>(menu, "_panel").activeSelf);
            yield return ClickGameButton(ui.Get<LobbyMenuOpenButton>().GetComponent<Button>());
            Assert.That(menu.IsOpen, Is.True);
            yield return ClickGameButton(Field<Button>(ui.Get<LobbyMenuPopupView>(), "_settingsButton"));
            Assert.That(ui.Get<LobbyMenuPopupView>().IsOpen, Is.False);
            Assert.That(ui.Get<SettingsPopupView>().IsOpen, Is.True);
            yield return ClickGameButton(Field<Button>(ui.Get<SettingsPopupView>(), "_closeButton"));
            yield return WaitFor(() => !ui.Get<SettingsPopupView>().IsOpen);
            // 오른쪽 메뉴의 친구·퀘스트·우편함 버튼이 미완성 안내 대신 각자의 팝업을 연다
            var closeField = typeof(LobbyListPopupView).GetField("_closeButton", BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (var popup in new LobbyListPopupView[] { ui.Get<FriendPopupView>(), ui.Get<QuestPopupView>(), ui.Get<MailPopupView>() })
            {
                var open = Object.FindObjectsByType<SideMenuPopupButton>(FindObjectsSortMode.None).Single(b => Field<LobbyListPopupView>(b, "_popup") == popup);
                yield return ClickGameButton(open.GetComponent<Button>());
                Assert.That(popup.IsOpen, Is.True, popup.name);
                yield return ClickGameButton((Button)closeField.GetValue(popup));
                yield return WaitFor(() => !popup.IsOpen);
            }
            Assert.That(Field<GameObject>(ui.Get<SkillScreenView>(), "_panel").activeSelf, Is.False);
            yield return ClickGameButton(Field<Button>(ui.Get<MissionButtonView>(), "_button"));
            var mission = ui.Get<MissionScreenView>();
            Assert.That(Field<GameObject>(mission, "_panel").activeSelf, Is.True);
            yield return ClickGameButton(Field<Button>(mission, "_infoButton"));
            var details = Field<MissionPopupView>(mission, "_detailsPopup");
            Assert.That(Field<GameObject>(details, "_panel").activeSelf, Is.True);
            // 몬스터·보상 칸을 실제로 눌러(레이캐스트) 공용 상세 창이 열리는지 본다
            var info = ui.Get<DetailPopupView>();
            SeedMonsterClaims(lobby.Container.Resolve<GameDataStore>(), lobby.Container.Resolve<PlayerProfile>());
            yield return ClickGameButton(FirstSlotButton(Field<StageMonsterListView>(details, "_monsters")));
            Assert.That(info.IsShown, Is.True);
            yield return ClaimMonsterAndCloseReward(ui, lobby.Container.Resolve<PlayerProfile>());
            yield return ClickGameButton(Field<Button>(info, "_closeButton"));
            Assert.That(info.IsShown, Is.False);
            yield return ClickGameButton(FirstSlotButton(Field<StageMonsterListView>(details, "_monsters")));
            Assert.That(Field<Button>(info, "_claimButton").gameObject.activeSelf, Is.False);
            yield return ClickGameButton(Field<Button>(info, "_closeButton"));
            yield return ClickGameButton(FirstSlotButton(Field<StageMonsterListView>(details, "_monsters"), 1));
            yield return ClaimMonsterAndCloseReward(ui, lobby.Container.Resolve<PlayerProfile>());
            yield return ClickGameButton(Field<Button>(info, "_closeButton"));
            yield return ClickGameButton(FirstSlotButton(Field<ResultRewardListView>(details, "_rewards")));
            Assert.That(info.IsShown, Is.True);
            yield return ClickGameButton(Field<Button>(info, "_closeButton"));
            Assert.That(info.IsShown, Is.False);
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
            // 일시정지 창의 등장 요마·확보 보상 칸을 실제로 눌러 공용 상세 창이 열리는지 본다
            var stageInfo = stageUi.Get<DetailPopupView>();
            var pause = stageUi.Get<PauseMenuView>();
            // 일시정지 수령은 새 미수령 프로필로 독립 검증한다.
            SeedMonsterClaims(stage.Container.Resolve<GameDataStore>(), stage.Container.Resolve<PlayerProfile>());
            yield return ClickGameButton(FirstSlotButton(Field<StageMonsterListView>(pause, "_monsterList")));
            Assert.That(stageInfo.IsShown, Is.True);
            yield return ClaimMonsterAndCloseReward(stageUi, stage.Container.Resolve<PlayerProfile>());
            Assert.That(manager.State, Is.EqualTo(StageState.Paused));
            Assert.That(Time.timeScale, Is.Zero);
            yield return ClickGameButton(Field<Button>(stageInfo, "_closeButton"));
            yield return ClickGameButton(FirstSlotButton(Field<ResultRewardListView>(pause, "_obtainableList")));
            Assert.That(stageInfo.IsShown, Is.True);
            yield return ClickGameButton(Field<Button>(stageInfo, "_closeButton"));
            Assert.That(stageInfo.IsShown, Is.False);
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
            wall.TakeDamage(wall.CurrentHp);
            yield return WaitFor(() => manager.State == StageState.Finished);
            Assert.That(wall.IsDestroyed, Is.True);
            var result = stageUi.Get<ResultPopupView>();
            Assert.That(Field<TMP_Text>(result, "_titleText").text, Is.EqualTo("Fail !"));
            var tip = Field<ResultTipView>(result, "_tip");
            Assert.That(tip.IsShown, Is.True);
            int tipClosed = 0;
            System.Action onTipClosed = () => tipClosed++;
            tip.Closed += onTipClosed;
            try
            {
                ExecuteEvents.Execute(tip.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);
                tip.OnPointerClick(new PointerEventData(EventSystem.current));
                Assert.That(tip.IsShown, Is.False);
                Assert.That(tipClosed, Is.EqualTo(1), "반복 닫기는 알림을 중복 발행하지 않는다");
            }
            finally { tip.Closed -= onTipClosed; }
            // 결과 창의 보상 칸도 실제로 눌러 상세 창이 열리는지 본다
            yield return ClickGameButton(FirstSlotButton(Field<ResultRewardListView>(result, "_rewardList")));
            Assert.That(stageInfo.IsShown, Is.True);
            yield return ClickGameButton(Field<Button>(stageInfo, "_closeButton"));
            Assert.That(stageInfo.IsShown, Is.False);
            yield return ClickGameButton(Field<Button>(result, "_lobbyButton"));
            yield return WaitFor(() => SceneManager.GetActiveScene().name == "Lobby");
            yield return null;
            Assert.That(Time.timeScale, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<UiHost>().Length, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator AllAuthoredWavesClearAndPersistProgress()
        {
            yield return StartFromBoot();
            yield return Object.FindAnyObjectByType<LobbyLifetimeScope>().Container.Resolve<BattleLauncher>().Launch(1).ToCoroutine();
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
                    var slots = Field<CardSlotView[]>(ui.Get<CardSelectView>(), "_slots");
                    yield return ClickGameButton(Field<Button>(slots.First(slot => slot.gameObject.activeSelf), "_button"));
                    yield return WaitFor(() => manager.State != StageState.CardSelect);
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
            var profile = Object.FindAnyObjectByType<LobbyLifetimeScope>().Container.Resolve<PlayerProfile>();
            Assert.That(profile.IsStageCleared(1), Is.True);
            Assert.That(profile.HighestUnlockedStage, Is.GreaterThanOrEqualTo(2));
            Assert.That(File.Exists(_savePath), Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<UiHost>().Length, Is.EqualTo(1));
        }

        // seed가 없으면 튜토리얼을 마친 계정으로 시작한다. 튜토리얼 중인 새 계정은 로비 대신 1스테이지로 들어간다
        private IEnumerator StartFromBoot(Action<LocalSave> seed = null, string expectedScene = "Lobby")
        {
            _hadSpeedPreference = PlayerPrefs.HasKey("stage_speed");
            _speedPreference = PlayerPrefs.GetFloat("stage_speed");
            _restoreSpeedPreference = true;
            var root = VContainer.Unity.VContainerSettings.Instance.GetOrCreateRootLifetimeScopeInstance() as RootLifetimeScope;
            Assert.That(root, Is.Not.Null);
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
            var navigator = root.Container.Resolve<ISceneNavigator>();
            var boot = new BootFlow(navigator);
            SceneManager.LoadSceneAsync("Boot");
            yield return WaitFor(() => SceneManager.GetActiveScene().name == "Boot");
            boot.Start();
            // Loading의 로그인창에서 게스트로 시작한다
            yield return WaitFor(() => SceneManager.GetActiveScene().name == "Loading" && LoginPanelShown());
            yield return ClickGameButton(Field<Button>(Object.FindAnyObjectByType<LoginView>(), "_guestButton"));
            yield return WaitFor(() => SceneManager.GetActiveScene().name == expectedScene && navigator.Current.ToString() == expectedScene);
            yield return null;
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
            }
            if (_credentials != null)
            {
                if (_hadCredential) { _credentials.Save(_credentialMethod, _credentialAccountId); }
                else { _credentials.Clear(); }
                _credentials = null;
            }
            if (_savePath != null && File.Exists(_savePath)) { File.Delete(_savePath); }
            _store = null;
            _originalSavePath = null;
            _savePath = null;
        }

        private void SeedMonsterClaims(GameDataStore data, PlayerProfile profile)
        {
            var save = _store.Load();
            save.codex = data.StageMonsters(1).Select(monster => new CodexRow { monsterId = monster.Id }).ToList();
            _store.Flush(save);
            profile.Apply(new LocalPlayerApi(_store).GetMe().GetAwaiter().GetResult());
        }

        private IEnumerator ClaimMonsterAndCloseReward(IUiRegistry ui, PlayerProfile profile)
        {
            var detail = ui.Get<DetailPopupView>();
            int monsterId = Field<int>(detail, "_claimMonsterId");
            int gold = profile.Gold;
            var reward = ui.Get<RewardPopupView>();
            var panel = Field<GameObject>(reward, "_panel");
            yield return ClickGameButton(Field<Button>(detail, "_claimButton"));
            yield return WaitFor(() => panel.activeSelf && Field<PopupTransition>(reward, "_transition").Progress >= 0.99f);
            Assert.That(detail.IsShown, Is.True);
            Assert.That(profile.CanClaimMonsterReward(monsterId), Is.False);
            Assert.That(profile.Gold, Is.GreaterThan(gold));
            Assert.That(_store.Load().wallet.gold, Is.EqualTo(profile.Gold));
            Assert.That(Field<Button>(detail, "_claimButton").gameObject.activeSelf, Is.False);
            // 상세창을 먼저 닫지 않고 보상창의 최상위 포인터 대상과 실제 닫기를 검증한다.
            // 등장 연출 중 탭은 연출 스킵이므로, 끝난 뒤 탭해서 닫는다
            yield return WaitFor(() => !reward.IsPlaying);
            yield return ClickGameButton(Field<Button>(reward, "_tapArea"));
            yield return WaitFor(() => !panel.activeSelf);
            Assert.That(detail.IsShown, Is.True);
        }

        private static Button FirstSlotButton(Component listView, int index = 0)
        {
            if (listView is ResultRewardListView rewards)
            {
                return rewards.Cells[index].GetComponent<Button>();
            }

            var slots = (System.Array)listView.GetType().GetField("_slots", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(listView);
            var slot = slots.GetValue(index);
            return ((GameObject)slot.GetType().GetField("Root").GetValue(slot)).GetComponent<Button>();
        }

        private static IEnumerator ClickGameButton(Button button, RectTransform hitArea = null)
        {
            // A focused tool/preview can temporarily supply Screen's editor dimensions.
            // Overlay raycasters must read the same Game View size as the canvas.
#if UNITY_EDITOR
            var gameView = Resources.FindObjectsOfTypeAll<EditorWindow>()
                .FirstOrDefault(window => window.GetType().FullName == "UnityEditor.GameView");
            gameView?.Focus();
#endif
            yield return null;
            yield return WaitFor(() => button != null && button.gameObject.activeInHierarchy && button.IsInteractable());
            Canvas.ForceUpdateCanvases();
            var canvas = button.GetComponentInParent<Canvas>();
            var rect = hitArea != null ? hitArea : (RectTransform)button.transform;
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
                hits.Clear();
                EventSystem.current.RaycastAll(pointer, hits);
                sampled = hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == button;
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
                        $"{button.name} did not become clickable. Top target={hits.FirstOrDefault().gameObject?.name}, Screen={Screen.width}x{Screen.height}");
                    yield return null;
                }
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
