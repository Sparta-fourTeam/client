using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Core;
using Game.Core.Messages;
using MessagePipe;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class EnergyRecoveryTests
    {
        // 서버 응답을 테스트에서 정한다. ToThrow가 있으면 거절, 없으면 Response를 돌려준다
        private sealed class FakeEnergyApi : IEnergyApi
        {
            public PlayerSnapshot Response;
            public ApiException ToThrow;
            public EnergySource LastSource;
            public string LastKey;
            public int CallCount;

            public UniTask<PlayerSnapshot> Recover(EnergySource source, string idempotencyKey = null)
            {
                CallCount++;
                LastSource = source;
                LastKey = idempotencyKey;
                if (ToThrow != null)
                {
                    throw ToThrow;
                }

                return UniTask.FromResult(Response);
            }
        }

        private static readonly DateTime T0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private PlayerProfile _profile;
        private FakeEnergyApi _api;
        private List<LobbyRequestFailed> _failed;
        private IDisposable _capture;
        private EnergyRecovery _recovery;

        [SetUp]
        public void SetUp()
        {
            var builder = new BuiltinContainerBuilder();
            builder.AddMessagePipe();
            builder.AddMessageBroker<LobbyRequestFailed>();
            IServiceProvider provider = builder.BuildServiceProvider();

            _failed = new List<LobbyRequestFailed>();
            _capture = provider.GetRequiredService<ISubscriber<LobbyRequestFailed>>().Subscribe(m => _failed.Add(m));

            _profile = new PlayerProfile(new GameDataStore());
            _api = new FakeEnergyApi();
            _recovery = new EnergyRecovery(_profile, _api, provider.GetRequiredService<IPublisher<LobbyRequestFailed>>());

            _profile.Apply(Snapshot(2));
        }

        [TearDown]
        public void TearDown()
        {
            _capture.Dispose();
        }

        private static PlayerSnapshot Snapshot(int energy)
        {
            return new PlayerSnapshot
            {
                gold = 100,
                energyStored = energy,
                energyUpdatedAt = T0.ToString("O"),
                stageProgress = new(),
                upgrades = new(),
            };
        }

        // FakeEnergyApi가 바로 끝나므로 동기로 기다려도 멈추지 않는다
        private bool Recover(EnergySource source)
        {
            return _recovery.Recover(source).GetAwaiter().GetResult();
        }

        [Test(Description = "광고 회복에 성공하면 서버 스냅샷으로 프로필을 바꾸고 true를 돌려준다")]
        public void Recover_Ad_Success_AppliesSnapshot()
        {
            _api.Response = Snapshot(7);

            bool ok = Recover(EnergySource.Ad);

            Assert.IsTrue(ok);
            Assert.AreEqual(1, _api.CallCount);
            Assert.AreEqual(EnergySource.Ad, _api.LastSource);
            Assert.AreEqual(7, _profile.EnergyStored);
            Assert.AreEqual(0, _failed.Count);
        }

        [Test(Description = "광고 회복은 중복 지급 방지용 키를 함께 보낸다")]
        public void Recover_Ad_SendsIdempotencyKey()
        {
            _api.Response = Snapshot(7);

            Recover(EnergySource.Ad);

            Assert.IsFalse(string.IsNullOrEmpty(_api.LastKey));
        }

        [Test(Description = "구매 회복에 성공하면 서버 스냅샷으로 프로필을 바꾸고, 키는 보내지 않는다")]
        public void Recover_Purchase_Success_AppliesSnapshotWithoutKey()
        {
            _api.Response = Snapshot(17);

            bool ok = Recover(EnergySource.Purchase);

            Assert.IsTrue(ok);
            Assert.AreEqual(EnergySource.Purchase, _api.LastSource);
            Assert.IsNull(_api.LastKey);
            Assert.AreEqual(17, _profile.EnergyStored);
        }

        [Test(Description = "회복이 거절되면 LobbyRequestFailed를 발행하고 false를 돌려주며, 프로필은 그대로 둔다")]
        public void Recover_Rejected_PublishesFailedAndKeepsProfile()
        {
            _api.ToThrow = new ApiException(ApiErrorKind.Rejected, "AD_NOT_READY");

            bool ok = Recover(EnergySource.Ad);

            Assert.IsFalse(ok);
            Assert.AreEqual(1, _failed.Count);
            Assert.AreEqual("AD_NOT_READY", _failed[0].Code);
            Assert.AreEqual(2, _profile.EnergyStored);
        }

        [Test(Description = "회복에 성공하면 프로필 Changed 이벤트가 불린다 (EnergyClock·LobbyModel이 이를 보고 갱신한다)")]
        public void Recover_Success_RaisesProfileChanged()
        {
            _api.Response = Snapshot(7);
            int changed = 0;
            _profile.Changed += _ => changed++;

            Recover(EnergySource.Ad);

            Assert.AreEqual(1, changed);
        }
    }
}
