using System;
using Game.Core;
using Game.Core.Messages;
using MessagePipe;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class BattleStatsTests
    {
        private IPublisher<EnemyDied> _enemyDied;
        private IPublisher<WaveGaugeChanged> _waveGaugeChanged;
        private IPublisher<StageEnded> _stageEnded;
        private BattleStats _stats;

        [SetUp]
        public void SetUp()
        {
            var builder = new BuiltinContainerBuilder();
            builder.AddMessagePipe();
            builder.AddMessageBroker<EnemyDied>();
            builder.AddMessageBroker<WaveGaugeChanged>();
            builder.AddMessageBroker<StageEnded>();
            builder.AddMessageBroker<WallHpChanged>();
            IServiceProvider provider = builder.BuildServiceProvider();

            _enemyDied = provider.GetRequiredService<IPublisher<EnemyDied>>();
            _waveGaugeChanged = provider.GetRequiredService<IPublisher<WaveGaugeChanged>>();
            _stageEnded = provider.GetRequiredService<IPublisher<StageEnded>>();

            _stats = new BattleStats(
                provider.GetRequiredService<ISubscriber<EnemyDied>>(),
                provider.GetRequiredService<ISubscriber<WaveGaugeChanged>>(),
                provider.GetRequiredService<ISubscriber<StageEnded>>(),
                provider.GetRequiredService<ISubscriber<WallHpChanged>>());
            _stats.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            _stats.Dispose();
        }

        // 게이지가 waveIndex 웨이브를 채우기 시작한 상황
        private void GaugeOnWave(int waveIndex)
        {
            _waveGaugeChanged.Publish(new WaveGaugeChanged(waveIndex, 0, 5));
        }

        private void EndStage()
        {
            _stageEnded.Publish(new StageEnded(StageOutcome.Clear));
        }

        [Test(Description = "시작 직후에는 모든 기록이 0이다")]
        public void Initial_AllZero()
        {
            Assert.AreEqual(0, _stats.Kills);
            Assert.AreEqual(0, _stats.ReachedWave);
            Assert.AreEqual(0f, _stats.PlayTime);
            Assert.IsFalse(_stats.IsEnded);
        }

        [Test(Description = "적이 죽을 때마다 처치 수가 1씩 오른다")]
        public void EnemyDied_IncreasesKills()
        {
            _enemyDied.Publish(new EnemyDied());
            _enemyDied.Publish(new EnemyDied());

            Assert.AreEqual(2, _stats.Kills);
        }

        [Test(Description = "게이지가 채우는 웨이브 중 가장 높은 번호를 도달 웨이브로 남긴다")]
        public void WaveGaugeChanged_KeepsHighestGaugeWave()
        {
            GaugeOnWave(1);
            GaugeOnWave(2);
            GaugeOnWave(3);

            Assert.AreEqual(3, _stats.ReachedWave);
        }

        [Test(Description = "흐른 시간만큼 플레이 시간이 쌓인다")]
        public void Advance_AccumulatesPlayTime()
        {
            _stats.Advance(1.5f);
            _stats.Advance(0.5f);

            Assert.AreEqual(2f, _stats.PlayTime, 0.0001f);
        }

        [Description("0 이하의 시간(일시정지 등)은 플레이 시간에 더하지 않는다")]
        [TestCase(0f)]
        [TestCase(-1f)]
        public void Advance_NonPositive_IsIgnored(float deltaTime)
        {
            _stats.Advance(deltaTime);

            Assert.AreEqual(0f, _stats.PlayTime);
        }

        [Test(Description = "스테이지가 끝나면 이후 처치·웨이브·시간은 기록하지 않는다")]
        public void AfterStageEnded_StopsRecording()
        {
            _enemyDied.Publish(new EnemyDied());
            GaugeOnWave(2);
            _stats.Advance(10f);

            EndStage();
            _enemyDied.Publish(new EnemyDied());
            GaugeOnWave(3);
            _stats.Advance(5f);

            Assert.IsTrue(_stats.IsEnded);
            Assert.AreEqual(1, _stats.Kills);
            Assert.AreEqual(2, _stats.ReachedWave);
            Assert.AreEqual(10f, _stats.PlayTime, 0.0001f);
        }

        [Test(Description = "Dispose 뒤에는 메시지를 받아도 기록하지 않는다")]
        public void AfterDispose_IgnoresMessages()
        {
            _stats.Dispose();

            _enemyDied.Publish(new EnemyDied());
            GaugeOnWave(2);

            Assert.AreEqual(0, _stats.Kills);
            Assert.AreEqual(0, _stats.ReachedWave);
        }

        [Test(Description = "고른 카드는 선택한 순서대로 빌드 로그에 쌓인다")]
        public void RecordCard_AppendsToBuildLog()
        {
            _stats.RecordCard("arrow_sharp");
            _stats.RecordCard("weapon_2");

            CollectionAssert.AreEqual(new[] { "arrow_sharp", "weapon_2" }, _stats.BuildLog);
        }
    }
}
