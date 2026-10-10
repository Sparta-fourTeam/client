using System.Reflection;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Core;
using Game.View;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests
{
    public sealed class MissionScreenTests
    {
        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        private GameObject _root;
        private GameObject _rewardRoot;
        private MissionScreenView _screen;
        private PlayerProfile _profile;
        private GameDataStore _data;
        private DeferredStageApi _api;

        private sealed class DeferredStageApi : IStageApi
        {
            public int Calls;
            public bool Fail;
            public readonly UniTaskCompletionSource<PlayerSnapshot> Response = new();

            public UniTask<PlayerSnapshot> ClaimRatingReward(int stageId)
            {
                Calls++;
                if (Fail) { throw new ApiException(ApiErrorKind.Network, "OFFLINE"); }
                return Response.Task;
            }
        }

        private sealed class DeferredBattleApi : IBattleApi
        {
            public int Calls;
            public readonly UniTaskCompletionSource<StartBattleResponse> Response = new();
            public UniTask<StartBattleResponse> StartBattle(int stageId, int dataRevision)
            {
                Calls++;
                return Response.Task;
            }
            public UniTask<SubmitResultResponse> SubmitResult(SubmitResultRequest request) =>
                throw new System.NotSupportedException();
        }

        private sealed class FailingNavigator : ISceneNavigator
        {
            public SceneId Current => SceneId.Lobby;
            public UniTask GoToStage(int stageId) => UniTask.FromException(new System.InvalidOperationException("scene failed"));
            public UniTask GoToLoading() => throw new System.NotSupportedException();
            public UniTask GoToLobby() => throw new System.NotSupportedException();
            public UniTask RestartStage() => throw new System.NotSupportedException();
        }

        private static void Set(object target, string name, object value) =>
            target.GetType().GetField(name, Private).SetValue(target, value);
        private static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
        private UniTask Claim() => (UniTask)typeof(MissionScreenView).GetMethod("ClaimRewardsAsync", Private).Invoke(_screen, null);

        private static PlayerSnapshot Snapshot(int clear, int claimed, int gold = 0) => new()
        {
            gold = gold,
            stageProgress = new() { new StageProgressRow { stageId = 1, clearRating = clear, claimedRating = claimed } },
            items = new(),
            upgrades = new()
        };

        [SetUp]
        public void SetUp()
        {
            _data = new GameDataStore();
            _profile = new PlayerProfile(_data);
            _profile.Apply(Snapshot(3, 1));
            _api = new DeferredStageApi();
            _root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/MissionScreen.prefab"));
            _screen = _root.GetComponent<MissionScreenView>();
            Set(_screen, "_profile", _profile);
            Set(_screen, "_data", _data);
            Set(_screen, "_stageApi", _api);
            _rewardRoot = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/RewardPopup.prefab"));
            Set(_screen, "_rewardPopup", _rewardRoot.GetComponent<RewardPopupView>());
            typeof(MissionScreenView).GetMethod("Awake", Private).Invoke(_screen, null);
            _screen.Open();
        }

        [TearDown]
        public void TearDown()
        {
            DOTween.KillAll();
            UnityEngine.Object.DestroyImmediate(_root);
            UnityEngine.Object.DestroyImmediate(_rewardRoot);
        }

        [Test]
        public void PendingClaim_BlocksRepeatedRequestsAndNavigation_ThenUsesReturnedSnapshot()
        {
            var pending = Claim();
            Claim().GetAwaiter().GetResult();
            typeof(MissionScreenView).GetMethod("Move", Private).Invoke(_screen, new object[] { 1 });
            Assert.AreEqual(1, _api.Calls);
            Assert.AreEqual(1, Get<int>(_screen, "_stageId"));
            Assert.IsFalse(Get<UnityEngine.UI.Button>(_screen, "_enterButton").interactable);

            _api.Response.TrySetResult(Snapshot(3, 3, 150));
            pending.GetAwaiter().GetResult();

            Assert.AreEqual(150, _profile.Gold);
            Assert.AreEqual(3, _profile.ClaimedRating(1));
            foreach (var node in Get<MissionRewardNodeView[]>(_screen, "_rewardNodes"))
            {
                Assert.IsTrue(Get<GameObject>(node, "_check").activeSelf);
                Assert.IsTrue(node.Button.interactable);
            }
            Assert.IsTrue(Get<GameObject>(Get<RewardPopupView>(_screen, "_rewardPopup"), "_panel").activeSelf);
        }

        [Test]
        public void FailedClaim_PreservesProgressAndAllowsRetry()
        {
            _api.Fail = true;
            Claim().GetAwaiter().GetResult();
            Assert.AreEqual(1, _profile.ClaimedRating(1));
            Assert.AreEqual(0, _profile.Gold);
            Assert.IsTrue(Get<MissionRewardNodeView[]>(_screen, "_rewardNodes")[1].Button.interactable);

            _api.Fail = false;
            _api.Response.TrySetResult(Snapshot(3, 3, 150));
            Claim().GetAwaiter().GetResult();
            Assert.AreEqual(2, _api.Calls);
            Assert.AreEqual(3, _profile.ClaimedRating(1));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void LaunchFault_RestoresInputsAndAllowsClosing(bool networkFailure)
        {
            var battleApi = new DeferredBattleApi();
            Set(_screen, "_launcher", new BattleLauncher(battleApi, new StageContext(), new FailingNavigator(), null));
            Set(_screen, "_energy", 100);
            var enter = Get<UnityEngine.UI.Button>(_screen, "_enterButton");
            enter.onClick.Invoke();
            enter.onClick.Invoke();
            Assert.AreEqual(1, battleApi.Calls);
            Assert.IsFalse(Get<UnityEngine.UI.Button>(_screen, "_backButton").interactable);

            UnityEngine.TestTools.LogAssert.Expect(LogType.Exception,
                new System.Text.RegularExpressions.Regex(networkFailure ? "OFFLINE" : "scene failed"));
            if (networkFailure) { battleApi.Response.TrySetException(new ApiException(ApiErrorKind.Network, "OFFLINE")); }
            else { battleApi.Response.TrySetResult(new StartBattleResponse { battleId = "test", seed = 1 }); }

            Assert.IsFalse(Get<bool>(_screen, "_launching"));
            Assert.IsTrue(enter.interactable);
            Assert.IsTrue(Get<UnityEngine.UI.Button>(_screen, "_infoButton").interactable);
            Assert.IsTrue(Get<UnityEngine.UI.Button>(_screen, "_rewardAreaButton").interactable);
            StringAssert.Contains("입장하지 못했어요", Get<TMPro.TMP_Text>(_screen, "_rewardStatus").text);
            var back = Get<UnityEngine.UI.Button>(_screen, "_backButton");
            Assert.IsTrue(back.interactable);
            back.onClick.Invoke();
            Assert.IsFalse(Get<GameObject>(_screen, "_panel").activeSelf);
            _screen.Open();
            Assert.IsTrue(enter.interactable);
        }

        [Test]
        public void NoReward_ChestClicksShowTheirOwnInformationWithoutCallingApi()
        {
            _profile.Apply(Snapshot(0, 0));
            _screen.Open();
            Claim().GetAwaiter().GetResult();
            Assert.AreEqual(0, _api.Calls);
            var nodes = Get<MissionRewardNodeView[]>(_screen, "_rewardNodes");
            var popup = Get<MissionRewardInfoPopupView>(_screen, "_rewardInfoPopup");
            string[] titles = { "관문 클리어", "방벽 체력 50%", "방벽 체력 100%" };
            for (int i = 0; i < nodes.Length; i++)
            {
                var node = nodes[i];
                Assert.IsTrue(node.Button.interactable);
                Assert.IsFalse(Get<GameObject>(node, "_check").activeSelf);
                Assert.IsFalse(Get<UnityEngine.UI.Image>(node, "_glow").gameObject.activeSelf);
                node.Button.onClick.Invoke();
                Assert.AreEqual(titles[i], Get<TMPro.TMP_Text>(popup, "_title").text);
                Assert.IsTrue(Get<GameObject>(popup, "_panel").activeSelf);
            }
            Assert.AreEqual(0, _api.Calls);
        }

        [Test]
        public void AnyPendingReward_OnlyAreaInputClaimsAllOnce()
        {
            _profile.Apply(Snapshot(2, 1));
            _screen.Open();
            var nodes = Get<MissionRewardNodeView[]>(_screen, "_rewardNodes");
            nodes[2].Button.onClick.Invoke();
            nodes[0].Button.onClick.Invoke();
            Assert.AreEqual(0, _api.Calls);
            var area = Get<UnityEngine.UI.Button>(_screen, "_rewardAreaButton");
            Assert.IsTrue(area.gameObject.activeSelf);
            area.onClick.Invoke();
            area.onClick.Invoke();
            Assert.AreEqual(1, _api.Calls);
            Assert.IsFalse(Get<GameObject>(Get<MissionRewardInfoPopupView>(_screen, "_rewardInfoPopup"), "_panel").activeSelf);
            _api.Response.TrySetResult(Snapshot(2, 2, 50));
            Assert.AreEqual(2, _profile.ClaimedRating(1));
            Assert.AreEqual(50, _profile.Gold);
            Assert.IsFalse(area.gameObject.activeSelf);
        }

        [Test]
        public void AllClaimed_ChestClickShowsInformationWithoutGrantingAgain()
        {
            _profile.Apply(Snapshot(3, 3, 170));
            _screen.Open();
            Get<MissionRewardNodeView[]>(_screen, "_rewardNodes")[1].Button.onClick.Invoke();
            Assert.AreEqual(0, _api.Calls);
            Assert.AreEqual(170, _profile.Gold);
            var popup = Get<MissionRewardInfoPopupView>(_screen, "_rewardInfoPopup");
            Assert.AreEqual("방벽 체력 50%", Get<TMPro.TMP_Text>(popup, "_title").text);
            StringAssert.Contains("이미 수령", Get<TMPro.TMP_Text>(popup, "_description").text);
        }

        [Test]
        public void Details_ChangingStageReplacesAndExpandsMonsterRows()
        {
            var popup = Get<MissionPopupView>(_screen, "_detailsPopup");
            popup.ShowDetails(_data, 2);
            var list = Get<StageMonsterListView>(popup, "_monsters");
            int active = 0;
            foreach (Transform slot in list.transform.Find("Slots")) { if (slot.gameObject.activeSelf) { active++; } }
            Assert.AreEqual(_data.Stages.GetOrThrow(2).MonsterIds.Count, active);
            popup.ShowDetails(_data, 1);
            active = 0;
            foreach (Transform slot in list.transform.Find("Slots")) { if (slot.gameObject.activeSelf) { active++; } }
            Assert.AreEqual(_data.Stages.GetOrThrow(1).MonsterIds.Count, active);
            Assert.AreEqual("제 1 관문", Get<TMPro.TMP_Text>(popup, "_title").text);
        }

        [Test]
        public void Glow_IsStaticAndOnlyVisibleForUnclaimedAchievedRewards()
        {
            var node = Get<MissionRewardNodeView[]>(_screen, "_rewardNodes")[1];
            var glow = Get<UnityEngine.UI.Image>(node, "_glow");
            node.Show(true, false, false);
            var color = glow.color;
            var size = glow.rectTransform.sizeDelta;
            Assert.IsTrue(glow.gameObject.activeSelf);
            Assert.AreEqual(0.85f, color.a, 0.001f);
            Assert.AreEqual(Vector3.one, glow.rectTransform.localScale);

            node.Show(true, false, true);
            typeof(MissionRewardNodeView).GetMethod("LateUpdate", Private).Invoke(node, null);
            Assert.AreEqual(color, glow.color);
            Assert.AreEqual(size, glow.rectTransform.sizeDelta);
            node.Show(true, true, false);
            Assert.IsFalse(glow.gameObject.activeSelf);
            node.Show(false, false, false);
            Assert.IsFalse(glow.gameObject.activeSelf);
        }

        [Test]
        public void Receipt_ShowsOnlyNewlyGrantedAmounts()
        {
            var before = Snapshot(2, 1, 50);
            before.items.Add(new ItemAmount { itemId = "book.arrow", quantity = 100 });
            var after = Snapshot(2, 2, 100);
            after.items.Add(new ItemAmount { itemId = "book.arrow", quantity = 103 });
            var rows = RewardRows.Gained(null, _data, before, after);
            Assert.AreEqual(2, rows.Count);
            Assert.AreEqual(50, rows[0].Count);
            Assert.AreEqual(3, rows[1].Count);
            Assert.IsEmpty(RewardRows.Gained(null, _data, after, after));
        }
    }
}
