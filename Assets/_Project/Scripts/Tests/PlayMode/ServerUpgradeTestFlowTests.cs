#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Boot;
using Game.Core;
using Game.Core.Messages;
using Game.Network;
using Game.View;
using MessagePipe;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using VContainer;
using Object = UnityEngine.Object;

namespace Game.Tests
{
    public sealed class ServerUpgradeTestFlowTests
    {
        private const string Id = "equipment.hat";
        private readonly List<GameObject> _missionParts = new();
        private bool _restoreMissionSelection, _hadMissionSelection;
        private int _missionSelection;
        private IServiceProvider _messageServices;
        private GameObject _popup;
        private GameObject _parentPopup;
        private DummyGrowthCatalog _catalog;
        private UpgradeConfirmPopupView _view;
        private IObjectResolver _container;
        private PlayerProfile _profile;
        private Transport _transport;
        private string _savePath;
        private byte[] _saved;
        private static T Field<T>(object target, string name) => (T)target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

        private void Create()
        {
            _savePath = Path.Combine(Path.GetTempPath(), $"server-upgrade-preserved-{Guid.NewGuid():N}.json");
            File.WriteAllText(_savePath, "existing-local-save-must-stay-untouched");
            _saved = File.ReadAllBytes(_savePath);
            var data = new GameDataStore();
            var definition = data.Upgrades.GetOrThrow(Id);
            definition.MaxLevel = 3; definition.UnlockLevel = 1;
            definition.LevelCosts = new[] { 10, 10, 10 }; definition.MaterialItemId = null;
            definition.Effects = new() { new() { Kind = "skillDamagePercent", ValuePerLevel = 9 } };
            _profile = new PlayerProfile(data);
            var session = new AuthSession(); session.Set("fixture-access", "fixture-refresh");
            _transport = new Transport(data.UnlockLevelOf(definition));
            var builder = new ContainerBuilder();
            builder.RegisterInstance(data);
            builder.RegisterInstance(new HttpApiConfig("https://example.test"));
            builder.RegisterInstance(_transport).As<IHttpTransport>();
            builder.RegisterInstance(session).As<IAuthTokenStore>();
            ServerUpgradeTestRegistration.Register(builder);
            _container = builder.Build();
            _profile.Apply(_container.Resolve<IPlayerApi>().GetMe().GetAwaiter().GetResult());
            _popup = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/UI/Lobby/UpgradeConfirmPopup.prefab"));
            _view = _popup.GetComponent<UpgradeConfirmPopupView>();
            _view.Construct(_profile, data, _container.Resolve<IUpgradeApi>());
        }

        private static IEnumerator Until(Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + 5;
            while (!condition() && Time.realtimeSinceStartup < deadline) { yield return null; }
            Assert.IsTrue(condition(), "비동기 UI 완료 조건이 5초 안에 충족되지 않았습니다");
        }

        [UnityTest]
        public IEnumerator Popup_LostResponse_ReopenRecoversSamePurchase_WithoutLocalMutation()
        {
            Create();
            yield return null; // 실제 Awake/프레임과 버튼 리스너를 사용한다.
            _view.Show("서버 강화", Id, true);
            Assert.IsFalse(_view.IsOpen, "서버 모드의 일괄 구매는 열지 않는다");
            _view.Show("서버 강화", Id, false);
            var confirm = Field<Button>(_view, "_confirmButton");
            confirm.onClick.Invoke();
            Assert.IsTrue(_view.IsBusy);
            Assert.IsFalse(confirm.interactable);
            confirm.onClick.Invoke();
            Assert.AreEqual(1, _transport.Posts.Count);
            _transport.Pending.TrySetResult(HttpResponseData.Failed(HttpFailure.Timeout));
            yield return Until(() => !_view.IsBusy);
            Assert.AreEqual(0, _profile.UpgradeLevel(Id));
            Field<Button>(_view, "_cancelButton").onClick.Invoke();
            _view.Show("다른 강화", "atk", false);
            confirm.onClick.Invoke();
            _transport.Pending.TrySetResult(new HttpResponseData(200, "{\"upgradeId\":\"equipment.hat\",\"level\":1}"));
            yield return Until(() => !_view.IsBusy);
            Assert.AreEqual(2, _transport.Posts.Count);
            Assert.AreEqual(_transport.Posts[0].Body, _transport.Posts[1].Body);
            Assert.AreEqual(990, _profile.Gold);
            Assert.AreEqual(1, _profile.UpgradeLevel(Id));
            Assert.AreEqual(1, new ProfileWeaponProgression(_profile).GetLevel(Id));
            Assert.IsNotEmpty(new ProfilePermanentSkillEffects(_profile, _container.Resolve<GameDataStore>()).For(null));
            CollectionAssert.AreEqual(_saved, File.ReadAllBytes(_savePath));
        }

        [UnityTest]
        public IEnumerator RegisteredMode_BlocksOtherMutations_AndAcknowledgedRecoveryOnlyReads()
        {
            Create(); _transport.FailSnapshot = true;
            yield return null;
            _view.Show("서버 강화", Id, false);
            var confirm = Field<Button>(_view, "_confirmButton");
            confirm.onClick.Invoke();
            _transport.Pending.TrySetResult(new HttpResponseData(200, "{\"upgradeId\":\"equipment.hat\",\"level\":1}"));
            yield return Until(() => !_view.IsBusy);
            Assert.AreEqual(1000, _profile.Gold);
            _transport.FailSnapshot = false;
            confirm.onClick.Invoke();
            yield return Until(() => !_view.IsBusy);
            Assert.AreEqual(1, _transport.Posts.Count);
            Assert.AreEqual(990, _profile.Gold);
            var calls = new Action[]
            {
                () => _container.Resolve<IBattleApi>().StartBattle(1).GetAwaiter().GetResult(),
                () => _container.Resolve<IBattleApi>().SubmitResult(null).GetAwaiter().GetResult(),
                () => _container.Resolve<IEnergyApi>().Recover(default).GetAwaiter().GetResult(),
                () => _container.Resolve<ICodexApi>().ClaimMonsterReward(1).GetAwaiter().GetResult(),
                () => _container.Resolve<IQuestApi>().ClaimQuest("q").GetAwaiter().GetResult(),
                () => _container.Resolve<IQuestApi>().ClaimQuestChest("c").GetAwaiter().GetResult(),
                () => _container.Resolve<IShopApi>().Reroll(default, "key").GetAwaiter().GetResult(),
                () => _container.Resolve<IShopApi>().Buy(0, "key").GetAwaiter().GetResult(),
                () => _container.Resolve<IShopApi>().BuyGoldPack(0, "key").GetAwaiter().GetResult(),
                () => _container.Resolve<IShopApi>().BuyGemPack(0, "key").GetAwaiter().GetResult(),
                () => _container.Resolve<ITutorialApi>().Complete("intro").GetAwaiter().GetResult(),
                () => _container.Resolve<IAccountApi>().DeleteAccount().GetAwaiter().GetResult(),
                () => _container.Resolve<IAccountApi>().LinkAccount(LoginMethod.Google, "id").GetAwaiter().GetResult(),
                () => _container.Resolve<IAccountApi>().ChangeNickname("name").GetAwaiter().GetResult(),
                () => _container.Resolve<IBattleCheckpointStore>().Save(null),
                () => _container.Resolve<IBattleCheckpointStore>().QueueSave(null),
            };
            foreach (var call in calls)
            { Assert.AreEqual(ServerUpgradeTestBackend.Disabled, Assert.Throws<ApiException>(() => call()).Code); }
            var checkpoint = _container.Resolve<IBattleCheckpointStore>();
            Assert.IsNull(checkpoint.Load()); checkpoint.Clear(); checkpoint.Flush();
            CollectionAssert.AreEqual(_saved, File.ReadAllBytes(_savePath));
            Assert.AreEqual(1, _transport.Posts.Count);
        }

        [UnityTest]
        public IEnumerator EquipmentEntry_ReopensPendingRecovery_WhenServerStateIsMaxedAndUnaffordable() => ParentRecovery(false);

        [UnityTest]
        public IEnumerator SkillEntry_ReopensPendingRecovery_WhenServerStateIsUnaffordable() => ParentRecovery(true);

        private IEnumerator ParentRecovery(bool skill)
        {
            Create(); yield return null;
            _view.Show("서버 강화", Id, false);
            Field<Button>(_view, "_confirmButton").onClick.Invoke();
            _transport.Pending.TrySetResult(HttpResponseData.Failed(HttpFailure.Timeout));
            yield return Until(() => !_view.IsBusy);
            Field<Button>(_view, "_cancelButton").onClick.Invoke();
            _transport.GoldAfterApply = 0; _transport.LevelAfterApply = 3;
            _profile.Apply(_container.Resolve<IPlayerApi>().GetMe().GetAwaiter().GetResult());
            var data = _container.Resolve<GameDataStore>();
            _catalog = new DummyGrowthCatalog(_profile, data);
            _parentPopup = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                $"Assets/_Project/Prefabs/UI/Lobby/{(skill ? "Skill" : "Equip")}UpgradePopup.prefab"));
            var registry = new UiRegistry(); registry.Add(_popup);
            Button entry;
            if (skill)
            {
                var parent = _parentPopup.GetComponent<SkillUpgradePopupView>();
                parent.Construct(_catalog, _profile, data); parent.BindUi(registry); parent.Open(0);
                entry = Field<Button>(parent, "_upgradeButton");
                Assert.IsFalse(Field<Button>(parent, "_upgradeAllButton").gameObject.activeSelf);
            }
            else
            {
                var parent = _parentPopup.GetComponent<EquipUpgradePopupView>();
                parent.Construct(_catalog, _profile, data); parent.BindUi(registry); parent.Open(0);
                entry = Field<Button>(parent, "_upgradeButton");
                Assert.IsFalse(Field<Button>(parent, "_upgradeAllButton").gameObject.activeSelf);
            }
            yield return null;
            Assert.IsTrue(entry.gameObject.activeInHierarchy && entry.interactable);
            entry.onClick.Invoke();
            Assert.IsTrue(_view.IsOpen);
            Field<Button>(_view, "_confirmButton").onClick.Invoke();
            _transport.Pending.TrySetResult(new HttpResponseData(200, "{\"upgradeId\":\"equipment.hat\",\"level\":1}"));
            yield return Until(() => !_view.IsBusy);
            Assert.AreEqual(2, _transport.Posts.Count);
            Assert.AreEqual(_transport.Posts[0].Body, _transport.Posts[1].Body);
            Assert.AreEqual(0, _profile.Gold);
            Assert.AreEqual(3, _profile.UpgradeLevel(Id));
        }

        private MissionScreenView CreateMission(out RewardPopupView reward)
        {
            _restoreMissionSelection = true;
            _hadMissionSelection = PlayerPrefs.HasKey("mission_selected_stage");
            _missionSelection = PlayerPrefs.GetInt("mission_selected_stage");
            PlayerPrefs.SetInt("mission_selected_stage", 1);
            var registry = new UiRegistry();
            foreach (string name in new[] { "EnergyRecoverPopup", "RewardPopup", "MissionDetailsPopup", "MissionRewardInfoPopup", "MissionScreen" })
            {
                var part = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Project/Prefabs/UI/Lobby/{name}.prefab"));
                _missionParts.Add(part); registry.Add(part);
            }
            reward = registry.Get<RewardPopupView>();
            var mission = registry.Get<MissionScreenView>();
            _messageServices = new BuiltinContainerBuilder().AddMessagePipe().AddMessageBroker<EnergyChanged>()
                .AddMessageBroker<StartFailed>().BuildServiceProvider();
            mission.Construct(_profile, _container.Resolve<GameDataStore>(), null,
                (IBufferedSubscriber<EnergyChanged>)_messageServices.GetService(typeof(IBufferedSubscriber<EnergyChanged>)),
                (ISubscriber<StartFailed>)_messageServices.GetService(typeof(ISubscriber<StartFailed>)),
                _container.Resolve<IStageApi>());
            mission.BindUi(registry); mission.Open();
            return mission;
        }

        [UnityTest]
        public IEnumerator MissionClaim_LostResponse_UsesGetOnlyAndNeverInfersRewardAmount()
        {
            Create(); _transport.KeepClaimUnconfirmed = true;
            var mission = CreateMission(out var reward);
            yield return null;
            var card = Field<StageCardView>(mission, "_card");
            card.RewardAreaButton.onClick.Invoke();
            card.RewardAreaButton.onClick.Invoke();
            Assert.AreEqual(1, _transport.Posts.Count);
            Assert.Throws<InvalidOperationException>(() => _container.Resolve<IUpgradeApi>().Purchase(Id, "other-key").GetAwaiter().GetResult());
            _transport.Pending.TrySetResult(HttpResponseData.Failed(HttpFailure.Timeout));
            yield return Until(() => !Field<bool>(mission, "_claiming"));
            Assert.That(card.Status, Does.Contain("미확정"));
            Assert.That(_profile.ClaimedRating(1), Is.EqualTo(0));
            var pending = (IStageRewardRecovery)_container.Resolve<IStageApi>();
            Assert.AreEqual(2, pending.PendingTargetRating);
            _view.Show("새 강화", Id, false);
            Assert.IsFalse(_view.IsOpen);
            _transport.ClaimClearRating = 3; _transport.ClaimedRating = 2;
            card.RewardAreaButton.onClick.Invoke();
            yield return Until(() => !Field<bool>(mission, "_claiming"));
            Assert.AreEqual(1, _transport.Posts.Count);
            Assert.AreEqual(2, _profile.ClaimedRating(1));
            Assert.That(card.Status, Does.Contain("서버에서 2등급까지 수령된 상태"));
            Assert.IsFalse(Field<GameObject>(reward, "_panel").activeSelf);
            CollectionAssert.AreEqual(_saved, File.ReadAllBytes(_savePath));
        }

        [UnityTest]
        public IEnumerator MissionClaim_AcknowledgedThenGetFails_ReopensRecoveryEvenWhenAllGradesAreClaimed()
        {
            Create(); _transport.FailSnapshot = true;
            var mission = CreateMission(out var reward);
            yield return null;
            var card = Field<StageCardView>(mission, "_card");
            card.RewardAreaButton.onClick.Invoke();
            _transport.Pending.TrySetResult(new HttpResponseData(200, "{\"stageId\":1,\"clearRating\":2,\"claimedRating\":2}"));
            yield return Until(() => !Field<bool>(mission, "_claiming"));
            Assert.AreEqual(0, _profile.ClaimedRating(1));
            _transport.FailSnapshot = false;
            _profile.Apply(_container.Resolve<IPlayerApi>().GetMe().GetAwaiter().GetResult());
            Assert.IsFalse(_profile.HasUnclaimedReward(1));
            Field<Button>(mission, "_backButton").onClick.Invoke();
            mission.Open();
            Assert.IsTrue(card.RewardAreaButton.gameObject.activeInHierarchy);
            card.RewardAreaButton.onClick.Invoke();
            yield return Until(() => !Field<bool>(mission, "_claiming"));
            Assert.AreEqual(1, _transport.Posts.Count);
            Assert.That(card.Status, Does.Contain("수령된 상태"));
            Assert.IsFalse(Field<GameObject>(reward, "_panel").activeSelf);
            CollectionAssert.AreEqual(_saved, File.ReadAllBytes(_savePath));
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (_popup != null)
            {
                foreach (var transition in _popup.GetComponentsInChildren<PopupTransition>(true)) { transition.HideImmediate(); }
                Object.Destroy(_popup);
            }
            foreach (var part in _missionParts)
            {
                foreach (var transition in part.GetComponentsInChildren<PopupTransition>(true)) { transition.HideImmediate(); }
                Object.Destroy(part);
            }
            _missionParts.Clear();
            if (_restoreMissionSelection)
            {
                if (_hadMissionSelection) { PlayerPrefs.SetInt("mission_selected_stage", _missionSelection); }
                else { PlayerPrefs.DeleteKey("mission_selected_stage"); }
                PlayerPrefs.Save();
                _restoreMissionSelection = false;
            }
            (_messageServices as IDisposable)?.Dispose();
            _messageServices = null;
            _container?.Dispose();
            _catalog?.Dispose();
            if (_parentPopup != null)
            {
                foreach (var transition in _parentPopup.GetComponentsInChildren<PopupTransition>(true)) { transition.HideImmediate(); }
                Object.Destroy(_parentPopup);
            }
            if (_savePath != null && File.Exists(_savePath)) { File.Delete(_savePath); }
            yield return null;
        }

        private sealed class Transport : IHttpTransport
        {
            private readonly int _unlock;
            private bool _applied, _claimAttempted;
            public bool KeepClaimUnconfirmed;
            public int ClaimClearRating = 2, ClaimedRating;
            public bool FailSnapshot;
            public int GoldAfterApply = 990, LevelAfterApply = 1;
            public readonly List<HttpRequestData> Posts = new();
            public UniTaskCompletionSource<HttpResponseData> Pending;
            public Transport(int unlock) => _unlock = unlock;
            public async UniTask<HttpResponseData> Send(HttpRequestData request, float timeoutSeconds, CancellationToken cancellationToken)
            {
                if (request.Url.EndsWith("/player-levels"))
                { return new HttpResponseData(200, "{\"baseExp\":10,\"stepExp\":5,\"maxLevel\":20,\"version\":2}"); }
                if (request.Verb == HttpVerb.Post)
                {
                    Posts.Add(request); Pending = new UniTaskCompletionSource<HttpResponseData>();
                    var response = await Pending.Task;
                    if (request.Url.EndsWith("/stage-progress/claim"))
                    {
                        _claimAttempted = true;
                        if (!KeepClaimUnconfirmed) { ClaimedRating = 2; }
                    }
                    else { _applied = true; } // 응답 유실 전에 이미 적용됐을 수 있다.
                    return response;
                }
                if (request.Url.EndsWith("/upgrade"))
                { return new HttpResponseData(200, $"[{{\"upgradeId\":\"equipment.hat\",\"maxLevel\":3,\"unlockLevel\":{_unlock},\"effectiveUnlockLevel\":{_unlock},\"costBase\":10}}]"); }
                if ((_applied || _claimAttempted) && FailSnapshot) { return new HttpResponseData(500, ""); }
                return new HttpResponseData(200, "{\"gold\":" + (_applied ? GoldAfterApply : _claimAttempted ? 875 : 1000)
                    + ",\"gem\":0,\"exp\":2147483647,\"energyStored\":10,\"energyUpdatedAt\":\"2026-10-16T23:00:00\","
                    + "\"upgrades\":[{\"upgradeId\":\"equipment.hat\",\"level\":" + (_applied ? LevelAfterApply : 0)
                    + "}],\"items\":[],\"stageProgress\":[{\"stageId\":1,\"clearRating\":" + ClaimClearRating
                    + ",\"claimedRating\":" + ClaimedRating + "}],\"codex\":[],\"tutorials\":[]}");
            }
        }
    }
}
#endif
