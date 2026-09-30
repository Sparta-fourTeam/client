using System;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Messages;
using MessagePipe;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class EnergyClockTests
    {
        private sealed class FakeClock : IUtcClock
        {
            public DateTime UtcNow { get; set; }
        }

        private sealed class FakeEnergyApi
        {

        }

        private static readonly DateTime T0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private FakeClock _clock;
        private PlayerProfile _profile;
        private List<EnergyChanged> _published;
        private IDisposable _capture;
        private EnergyClock _energyClock;

        private int Max => _profile.EnergyConfig.Max;
        private double RegenSeconds => _profile.EnergyConfig.RegenSeconds;

        [SetUp]
        public void SetUp()
        {
            var builder = new BuiltinContainerBuilder();
            builder.AddMessagePipe();
            builder.AddMessageBroker<EnergyChanged>();
            IServiceProvider provider = builder.BuildServiceProvider();

            // 일반 구독자로 받는다 (Buffered 구독은 struct 기본값이 먼저 와서 셈이 섞인다)
            _published = new List<EnergyChanged>();
            _capture = provider.GetRequiredService<ISubscriber<EnergyChanged>>().Subscribe(m => _published.Add(m));

            _clock = new FakeClock { UtcNow = T0 };
            _profile = new PlayerProfile(new GameDataStore());
            _energyClock = new EnergyClock(_profile, _clock, provider.GetRequiredService<IBufferedPublisher<EnergyChanged>>());
        }

        [TearDown]
        public void TearDown()
        {
            _capture.Dispose();
        }

        private void ApplyEnergy(int stored, DateTime updatedAt)
        {
            _profile.Apply(new PlayerSnapshot
            {
                energyStored = stored,
                energyUpdatedAt = updatedAt.ToString("O"),
                stageProgress = new(),
                upgrades = new(),
            });
        }

        [Test(Description = "처음 Tick하면 현재 에너지를 한 번 발행한다")]
        public void FirstTick_PublishesCurrentEnergy()
        {
            ApplyEnergy(7, T0);

            _energyClock.Tick();

            Assert.AreEqual(1, _published.Count);
            Assert.AreEqual(7, _published[0].Current);
            Assert.AreEqual(Max, _published[0].Max);
        }

        [Test(Description = "값이 그대로면 여러 번 Tick해도 다시 발행하지 않는다")]
        public void Tick_WhenValueUnchanged_DoesNotPublishAgain()
        {
            ApplyEnergy(7, T0);

            _energyClock.Tick();
            _clock.UtcNow = T0.AddSeconds(RegenSeconds - 1);
            _energyClock.Tick();
            _energyClock.Tick();

            Assert.AreEqual(1, _published.Count);
        }

        [Test(Description = "회복 주기가 지나면 1 오른 값으로 한 번 더 발행한다")]
        public void Tick_AfterRegenPeriod_PublishesIncreasedEnergy()
        {
            ApplyEnergy(7, T0);

            _energyClock.Tick();
            _clock.UtcNow = T0.AddSeconds(RegenSeconds);
            _energyClock.Tick();

            Assert.AreEqual(2, _published.Count);
            Assert.AreEqual(8, _published[1].Current);
        }

        [Test(Description = "오래 지나도 최대치에서 멈추고, 그 뒤로는 발행하지 않는다")]
        public void Tick_AfterLongTime_StopsAtMax()
        {
            ApplyEnergy(7, T0);

            _clock.UtcNow = T0.AddSeconds(RegenSeconds * Max * 2);
            _energyClock.Tick();
            _clock.UtcNow = _clock.UtcNow.AddHours(1);
            _energyClock.Tick();

            Assert.AreEqual(1, _published.Count);
            Assert.AreEqual(Max, _published[0].Current);
        }

        [Test(Description = "프로필이 새 값으로 바뀌면(스테이지 입장으로 소모 등) 다음 Tick에 바뀐 값을 발행한다")]
        public void Tick_AfterProfileChanged_PublishesNewValue()
        {
            ApplyEnergy(10, T0);
            _energyClock.Tick();

            ApplyEnergy(5, T0);
            _energyClock.Tick();

            Assert.AreEqual(2, _published.Count);
            Assert.AreEqual(5, _published[1].Current);
        }
    }
}
