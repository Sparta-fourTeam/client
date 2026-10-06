using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Core.Messages;
using MessagePipe;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class WaveProgressTests
    {
        private IServiceProvider _provider;
        private IPublisher<WaveStarted> _waveStarted;
        private IPublisher<EnemyDied> _enemyDied;
        private List<WaveGaugeChanged> _changed;
        private List<WaveGaugeFilled> _filled;
        private IDisposable _captures;
        private WaveProgress _progress;

        [SetUp]
        public void SetUp()
        {
            var builder = new BuiltinContainerBuilder();
            builder.AddMessagePipe();
            builder.AddMessageBroker<WaveStarted>();
            builder.AddMessageBroker<EnemyDied>();
            builder.AddMessageBroker<WaveGaugeChanged>();
            builder.AddMessageBroker<WaveGaugeFilled>();
            _provider = builder.BuildServiceProvider();

            _waveStarted = _provider.GetRequiredService<IPublisher<WaveStarted>>();
            _enemyDied = _provider.GetRequiredService<IPublisher<EnemyDied>>();

            // 일반 구독자로 받는다 (Buffered 구독은 struct 기본값이 먼저 와서 셈이 섞인다)
            _changed = new List<WaveGaugeChanged>();
            _filled = new List<WaveGaugeFilled>();
            DisposableBagBuilder bag = DisposableBag.CreateBuilder();
            _provider.GetRequiredService<ISubscriber<WaveGaugeChanged>>().Subscribe(m => _changed.Add(m)).AddTo(bag);
            _provider.GetRequiredService<ISubscriber<WaveGaugeFilled>>().Subscribe(m => _filled.Add(m)).AddTo(bag);
            _captures = bag.Build();

            _progress = new WaveProgress(
                _provider.GetRequiredService<ISubscriber<WaveStarted>>(),
                _provider.GetRequiredService<ISubscriber<EnemyDied>>(),
                _provider.GetRequiredService<IBufferedPublisher<WaveGaugeChanged>>(),
                _provider.GetRequiredService<IPublisher<WaveGaugeFilled>>());
            _progress.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            _progress.Dispose();
            _captures.Dispose();
        }

        private void StartWave(int enemyCount, bool isFinalWave = false, int waveIndex = 0)
        {
            _waveStarted.Publish(new WaveStarted(waveIndex, enemyCount, isFinalWave));
        }

        private void KillEnemies(int count)
        {
            for (int i = 0; i < count; i++)
            {
                _enemyDied.Publish(new EnemyDied());
            }
        }

        [Test(Description = "분열·소환으로 생긴 적의 사망은 게이지에 세지 않는다")]
        public void SummonedEnemyDied_DoesNotIncreaseGauge()
        {
            StartWave(3);
            int before = _changed.Count;

            _enemyDied.Publish(new EnemyDied(1, isSummoned: true));

            Assert.AreEqual(before, _changed.Count);
            Assert.AreEqual(0, _filled.Count);
        }

        [Test(Description = "웨이브가 시작되면 빈 게이지(0/몬스터 수)를 발행한다")]
        public void WaveStarted_PublishesEmptyGauge()
        {
            StartWave(3);

            Assert.AreEqual(1, _changed.Count);
            Assert.AreEqual(0, _changed[0].Current);
            Assert.AreEqual(3, _changed[0].Max);
        }

        [Test(Description = "적이 죽으면 게이지가 1 오른다")]
        public void EnemyDied_IncreasesGauge()
        {
            StartWave(3);

            KillEnemies(1);

            Assert.AreEqual(1, _changed.Last().Current);
            Assert.AreEqual(3, _changed.Last().Max);
            Assert.AreEqual(0, _filled.Count);
        }

        [Test(Description = "웨이브의 몬스터를 모두 잡으면 게이지 가득 메시지를 1번 발행한다")]
        public void AllEnemiesKilled_PublishesFilledOnce()
        {
            StartWave(3);

            KillEnemies(3);

            Assert.AreEqual(1, _filled.Count);
            Assert.IsFalse(_filled[0].IsFinalWave);
            Assert.AreEqual(3, _changed.Last().Current);
        }

        [Test(Description = "마지막 웨이브의 게이지가 차면 마지막 웨이브라고 알린다")]
        public void FinalWaveKilled_PublishesFinalFilled()
        {
            StartWave(2, isFinalWave: true);

            KillEnemies(2);

            Assert.AreEqual(1, _filled.Count);
            Assert.IsTrue(_filled[0].IsFinalWave);
        }

        [Test(Description = "앞 웨이브가 차기 전에 다음 웨이브가 시작돼도 게이지는 앞 웨이브 기준을 유지한다")]
        public void NextWaveStartedBeforeFill_KeepsCurrentGauge()
        {
            StartWave(3);
            KillEnemies(1);

            StartWave(5);

            Assert.AreEqual(2, _changed.Count);
            Assert.AreEqual(1, _changed.Last().Current);
            Assert.AreEqual(3, _changed.Last().Max);
        }

        [Test(Description = "앞 웨이브가 차면 이미 시작된 다음 웨이브 크기로 0부터 다시 채운다")]
        public void Filled_WithQueuedWave_StartsNextGauge()
        {
            StartWave(3);
            StartWave(5);

            KillEnemies(3);

            Assert.AreEqual(1, _filled.Count);
            Assert.AreEqual(0, _changed.Last().Current);
            Assert.AreEqual(5, _changed.Last().Max);
        }

        [Test(Description = "어느 웨이브 적이든 처치는 시작된 순서대로 게이지를 채운다")]
        public void Kills_FillWavesInOrder()
        {
            StartWave(2);
            StartWave(3, isFinalWave: true);

            KillEnemies(5);

            Assert.AreEqual(2, _filled.Count);
            Assert.IsFalse(_filled[0].IsFinalWave);
            Assert.IsTrue(_filled[1].IsFinalWave);
        }

        [Test(Description = "시작된 웨이브가 없을 때 죽은 적은 세지 않는다")]
        public void EnemyDied_BeforeAnyWave_IsIgnored()
        {
            KillEnemies(1);
            StartWave(3);

            Assert.AreEqual(1, _changed.Count);
            Assert.AreEqual(0, _changed[0].Current);
            Assert.AreEqual(0, _filled.Count);
        }

        [Description("몬스터가 0 이하인 웨이브는 게이지에 넣지 않는다")]
        [TestCase(0)]
        [TestCase(-1)]
        public void NonPositiveEnemyWave_IsIgnored(int enemyCount)
        {
            StartWave(enemyCount);
            StartWave(2);

            KillEnemies(2);

            Assert.AreEqual(2, _changed.First().Max);
            Assert.AreEqual(1, _filled.Count);
        }

        [Test(Description = "Dispose 뒤에는 웨이브 시작과 처치를 무시한다")]
        public void AfterDispose_IgnoresEverything()
        {
            _progress.Dispose();

            StartWave(1);
            KillEnemies(1);

            Assert.AreEqual(0, _changed.Count);
            Assert.AreEqual(0, _filled.Count);
        }

        [Test(Description = "늦게 구독한 쪽도 Buffered로 현재 게이지 값을 바로 받는다")]
        public void LateBufferedSubscriber_ReceivesCurrentGauge()
        {
            StartWave(3);
            KillEnemies(1);

            WaveGaugeChanged received = default;
            using (_provider.GetRequiredService<IBufferedSubscriber<WaveGaugeChanged>>().Subscribe(m => received = m))
            {
                Assert.AreEqual(1, received.Current);
                Assert.AreEqual(3, received.Max);
            }
        }

        [Test(Description = "게이지 메시지에는 지금 채우고 있는 웨이브 번호가 담긴다")]
        public void Gauge_CarriesWaveIndexOfFillingWave()
        {
            StartWave(3, waveIndex: 1);
            StartWave(5, waveIndex: 2);   // 2번 웨이브가 시작돼도

            KillEnemies(1);
            Assert.AreEqual(1, _changed.Last().WaveIndex);   // 게이지는 아직 1번 웨이브

            KillEnemies(2);                                    // 1번 웨이브 가득
            Assert.AreEqual(2, _changed.Last().WaveIndex);   // 이제 2번 웨이브
        }
    }
}
