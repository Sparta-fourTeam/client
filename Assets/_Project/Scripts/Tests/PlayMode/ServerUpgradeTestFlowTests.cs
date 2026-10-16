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
using Game.Network;
using Game.View;
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
        private GameObject _popup;
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
                () => _container.Resolve<IStageApi>().ClaimRatingReward(1).GetAwaiter().GetResult(),
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

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (_popup != null)
            {
                foreach (var transition in _popup.GetComponentsInChildren<PopupTransition>(true)) { transition.HideImmediate(); }
                Object.Destroy(_popup);
            }
            _container?.Dispose();
            if (_savePath != null && File.Exists(_savePath)) { File.Delete(_savePath); }
            yield return null;
        }

        private sealed class Transport : IHttpTransport
        {
            private readonly int _unlock;
            private bool _applied;
            public bool FailSnapshot;
            public readonly List<HttpRequestData> Posts = new();
            public UniTaskCompletionSource<HttpResponseData> Pending;
            public Transport(int unlock) => _unlock = unlock;
            public async UniTask<HttpResponseData> Send(HttpRequestData request, float timeoutSeconds, CancellationToken cancellationToken)
            {
                if (request.Verb == HttpVerb.Post)
                {
                    Posts.Add(request); Pending = new UniTaskCompletionSource<HttpResponseData>();
                    var response = await Pending.Task;
                    _applied = true; // fixture는 응답 유실 전에도 서버 적용이 끝난 경우를 만든다.
                    return response;
                }
                if (request.Url.EndsWith("/upgrade"))
                { return new HttpResponseData(200, $"[{{\"upgradeId\":\"equipment.hat\",\"maxLevel\":3,\"unlockLevel\":{_unlock},\"costBase\":10}}]"); }
                if (_applied && FailSnapshot) { return new HttpResponseData(500, ""); }
                return new HttpResponseData(200, "{\"gold\":" + (_applied ? 990 : 1000)
                    + ",\"gem\":0,\"exp\":2147483647,\"energyStored\":10,\"energyUpdatedAt\":\"2026-10-16T23:00:00\","
                    + "\"upgrades\":[{\"upgradeId\":\"equipment.hat\",\"level\":" + (_applied ? 1 : 0)
                    + "}],\"items\":[],\"stageProgress\":[],\"codex\":[],\"tutorials\":[]}");
            }
        }
    }
}
#endif
