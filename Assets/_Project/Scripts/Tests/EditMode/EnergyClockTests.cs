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

        [TestCase(6, 2, 1)]
        [TestCase(7, 3, 7)]
        [TestCase(14, 4, 7)]
        public void NaturalRecoveryUsesConfiguredInterval(int elapsedSeconds, int expectedEnergy, int expectedNextIn)
        {
            var config = new EnergyConfig { Max = 11, RegenSeconds = 7 };

            var (current, nextIn) = EnergyRule.At(2, T0, T0.AddSeconds(elapsedSeconds), config);

            Assert.That(current, Is.EqualTo(expectedEnergy));
            Assert.That(nextIn, Is.EqualTo(expectedNextIn));
        }

        [TestCase(0, 299, 0)]
        [TestCase(0, 300, 300)]
        [TestCase(0, 599, 300)]
        [TestCase(10, 599, 599)]
        [TestCase(0, -1, -1)]
        public void SettlingRegenPreservesRemainderAndResetsAtCap(int current, int elapsed, int expectedTimestamp)
        {
            var config = new EnergyConfig { Max = 10, RegenSeconds = 300 };
            Assert.That(EnergyRule.SettledUpdatedAt(current, T0, T0.AddSeconds(elapsed), config),
                Is.EqualTo(T0.AddSeconds(expectedTimestamp)));
        }

        [Test(Description = "첫 발행 후 회복 직전에는 무신호, 주기 경계에서 증가한 값을 한 번 발행한다")]
        public void TickPublishesRecoveryBoundariesCapAndChangedProfileWithoutDuplicates()
        {
            ApplyEnergy(7, T0);
            _energyClock.Tick();
            Assert.AreEqual(1, _published.Count);
            Assert.AreEqual(7, _published[0].Current);
            Assert.AreEqual(Max, _published[0].Max);

            _clock.UtcNow = T0.AddSeconds(RegenSeconds - 1);
            _energyClock.Tick();
            _energyClock.Tick();
            Assert.AreEqual(1, _published.Count);

            _clock.UtcNow = T0.AddSeconds(RegenSeconds);
            _energyClock.Tick();
            _energyClock.Tick();
            Assert.AreEqual(2, _published.Count);
            Assert.AreEqual(8, _published[1].Current);
            _clock.UtcNow = T0.AddSeconds(RegenSeconds * Max * 2);
            _energyClock.Tick();
            Assert.AreEqual(Max, _published[_published.Count - 1].Current);
            int count = _published.Count;
            _clock.UtcNow = _clock.UtcNow.AddHours(1);
            _energyClock.Tick();
            Assert.AreEqual(count, _published.Count);
            ApplyEnergy(5, _clock.UtcNow);
            _energyClock.Tick();
            Assert.AreEqual(count + 1, _published.Count);
            Assert.AreEqual(5, _published[_published.Count - 1].Current);
        }

    }
}
