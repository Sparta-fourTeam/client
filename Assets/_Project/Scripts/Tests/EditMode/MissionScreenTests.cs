using System.Reflection;
using Cysharp.Threading.Tasks;
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
        private GameObject _detailsRoot, _rewardInfoRoot;
        private MissionScreenView _screen;
        private PlayerProfile _profile;
        private GameDataStore _data;
        private DeferredStageApi _api;
        private bool _hadSelection;
        private int _savedSelection;

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
            public UniTask<StartBattleResponse> StartBattle(int stageId)
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
        private StageCardView Card => Get<StageCardView>(_screen, "_card");
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
            _hadSelection = PlayerPrefs.HasKey("mission_selected_stage");
            _savedSelection = PlayerPrefs.GetInt("mission_selected_stage");
            PlayerPrefs.DeleteKey("mission_selected_stage");
            _data = new GameDataStore();
            _profile = new PlayerProfile(_data);
            _profile.Apply(Snapshot(3, 1));
            _api = new DeferredStageApi();
            _root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Lobby/MissionScreen.prefab"));
            _screen = _root.GetComponent<MissionScreenView>();
            Set(_screen, "_profile", _profile);
            Set(_screen, "_data", _data);
            Set(_screen, "_stageApi", _api);
            _rewardRoot = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Lobby/RewardPopup.prefab"));
            Set(_screen, "_rewardPopup", _rewardRoot.GetComponent<RewardPopupView>());
            _detailsRoot = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Lobby/MissionDetailsPopup.prefab"));
            _rewardInfoRoot = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Lobby/MissionRewardInfoPopup.prefab"));
            var details = _detailsRoot.GetComponent<MissionPopupView>();
            var info = _rewardInfoRoot.GetComponent<MissionRewardInfoPopupView>();
            details.Initialize();
            info.Initialize();
            Set(_screen, "_detailsPopup", details);
            Set(_screen, "_rewardInfoPopup", info);
            _screen.Initialize();
            _screen.Open();
        }

        [TearDown]
        public void TearDown()
        {
            if (_hadSelection) { PlayerPrefs.SetInt("mission_selected_stage", _savedSelection); }
            else { PlayerPrefs.DeleteKey("mission_selected_stage"); }
            foreach (var root in new[] { _root, _rewardRoot })
            {
                if (root == null) { continue; }
                foreach (var transition in root.GetComponentsInChildren<PopupTransition>(true)) { transition.HideImmediate(); }
            }
            UnityEngine.Object.DestroyImmediate(_root);
            UnityEngine.Object.DestroyImmediate(_rewardRoot);
            UnityEngine.Object.DestroyImmediate(_detailsRoot);
            UnityEngine.Object.DestroyImmediate(_rewardInfoRoot);
        }

        [Test]
        public void FailedClaim_PreservesProgressAndAllowsRetry()
        {
            _api.Fail = true;
            Claim().GetAwaiter().GetResult();
            Assert.AreEqual(1, _profile.ClaimedRating(1));
            Assert.AreEqual(0, _profile.Gold);
            Assert.IsTrue(Get<MissionRewardNodeView[]>(Card, "_rewardNodes")[1].Button.interactable);

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
            else { battleApi.Response.TrySetResult(new StartBattleResponse { battleId = "test" }); }

            Assert.IsFalse(Get<bool>(_screen, "_launching"));
            Assert.IsTrue(enter.interactable);
            Assert.IsTrue(Get<UnityEngine.UI.Button>(Card, "_infoButton").interactable);
            Assert.IsTrue(Get<UnityEngine.UI.Button>(Card, "_rewardAreaButton").interactable);
            StringAssert.Contains("입장하지 못했어요", Get<TMPro.TMP_Text>(Card, "_rewardStatus").text);
            var back = Get<UnityEngine.UI.Button>(_screen, "_backButton");
            Assert.IsTrue(back.interactable);
            back.onClick.Invoke();
            Assert.IsFalse(Get<GameObject>(_screen, "_panel").activeSelf);
            _screen.Open();
            Assert.IsTrue(enter.interactable);
        }

        [Test]
        public void ClaimInputBlocksRepeatedRequestsAndNavigationAndAppliesReturnedSnapshot()
        {
            _profile.Apply(Snapshot(2, 1));
            _screen.Open();
            var nodes = Get<MissionRewardNodeView[]>(Card, "_rewardNodes");
            nodes[2].Button.onClick.Invoke();
            nodes[0].Button.onClick.Invoke();
            Assert.AreEqual(0, _api.Calls);
            var area = Get<UnityEngine.UI.Button>(Card, "_rewardAreaButton");
            Assert.IsTrue(area.gameObject.activeSelf);
            area.onClick.Invoke();
            area.onClick.Invoke();
            Assert.AreEqual(1, _api.Calls);
            typeof(MissionScreenView).GetMethod("Move", Private).Invoke(_screen, new object[] { 1 });
            Assert.AreEqual(1, Get<int>(_screen, "_stageId"));
            Assert.IsFalse(Get<UnityEngine.UI.Button>(_screen, "_enterButton").interactable);
            Assert.IsFalse(Get<GameObject>(Get<MissionRewardInfoPopupView>(_screen, "_rewardInfoPopup"), "_panel").activeSelf);
            _api.Response.TrySetResult(Snapshot(2, 2, 50));
            Assert.AreEqual(2, _profile.ClaimedRating(1));
            Assert.AreEqual(50, _profile.Gold);
            Assert.IsFalse(area.gameObject.activeSelf);
            Assert.IsTrue(Get<UnityEngine.UI.Button>(_screen, "_enterButton").interactable);
        }

        [Test]
        public void ReopeningMission_PreservesViewedStage_AndHighestSettingOnlyFocuses()
        {
            var snapshot = Snapshot(3, 3);
            snapshot.stageProgress.Add(new StageProgressRow { stageId = 2 });
            _profile.Apply(snapshot);
            var settings = new GameSettings(new PlayerPrefsSettingsStore());
            bool previous = settings.AutoEnterHighestStage;
            var battleApi = new DeferredBattleApi();
            Set(_screen, "_launcher", new BattleLauncher(battleApi, new StageContext(), new FailingNavigator(), null));
            Set(_screen, "_energy", 100);
            Set(_screen, "_settings", settings);
            try
            {
                settings.SetAutoEnterHighestStage(false);
                _screen.Open();
                Assert.AreEqual(1, Get<int>(_screen, "_stageId"));
                typeof(MissionScreenView).GetMethod("Move", Private).Invoke(_screen, new object[] { 1 });
                _screen.Open();
                Assert.AreEqual(2, Get<int>(_screen, "_stageId"));
                settings.SetAutoEnterHighestStage(true);
                _screen.Open();
                Assert.AreEqual(2, Get<int>(_screen, "_stageId"));
                Assert.AreEqual(0, battleApi.Calls, "최고 스테이지 설정은 입장 요청을 보내면 안 된다");
                Assert.IsTrue(Get<GameObject>(_screen, "_panel").activeSelf);
            }
            finally { settings.SetAutoEnterHighestStage(previous); }
        }

        // 지금 도전 중인 관문 뒤의 잠긴 관문은 넘겨 볼 수 없다 (화살표 숨김, 넘기기 무시)
        [Test]
        public void LockedStagesAfterCurrentAreNotReachable()
        {
            var snapshot = Snapshot(3, 3);
            snapshot.stageProgress.Add(new StageProgressRow { stageId = 2 });
            _profile.Apply(snapshot);
            var settings = new GameSettings(new PlayerPrefsSettingsStore());
            bool previous = settings.AutoEnterHighestStage;
            settings.SetAutoEnterHighestStage(true);
            Set(_screen, "_settings", settings);
            try { _screen.Open(); }
            finally { settings.SetAutoEnterHighestStage(previous); }
            Assert.AreEqual(2, _profile.HighestUnlockedStage);
            Assert.IsTrue(_data.Stages.Contains(3));
            Assert.AreEqual(2, Get<int>(_screen, "_stageId"));
            Assert.IsFalse(Get<UnityEngine.UI.Button>(_screen, "_nextButton").gameObject.activeSelf);
            Assert.IsTrue(Get<UnityEngine.UI.Button>(_screen, "_prevButton").gameObject.activeSelf);
            typeof(MissionScreenView).GetMethod("Move", Private).Invoke(_screen, new object[] { 1 });
            Assert.AreEqual(2, Get<int>(_screen, "_stageId"));
        }
    }
}
