using System;
using System.Collections.Generic;
using Game.Core.Messages;
using Game.Core.Stage;
using MessagePipe;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class StageJudgeTests
    {
        private IPublisher<WallDestroyed> _wallDestroyed;
        private IPublisher<WaveGaugeFilled> _waveGaugeFilled;
        private List<StageEnded> _ended;
        private IDisposable _endedSubscription;
        private StageJudge _judge;

        [SetUp]
        public void Setup()
        {
            var builder = new BuiltinContainerBuilder();
            builder.AddMessagePipe();
            builder.AddMessageBroker<WallDestroyed>();
            builder.AddMessageBroker<WaveGaugeFilled>();
            builder.AddMessageBroker<StageEnded>();
            IServiceProvider provider = builder.BuildServiceProvider();

            _wallDestroyed = provider.GetRequiredService<IPublisher<WallDestroyed>>();
            _waveGaugeFilled = provider.GetRequiredService<IPublisher<WaveGaugeFilled>>();

            _ended = new List<StageEnded>();
            _endedSubscription = provider.GetRequiredService<ISubscriber<StageEnded>>()
                .Subscribe(m => _ended.Add(m));

            _judge = new StageJudge(
                provider.GetRequiredService<ISubscriber<WallDestroyed>>(),
                provider.GetRequiredService<ISubscriber<WaveGaugeFilled>>(),
                provider.GetRequiredService<IPublisher<StageEnded>>());
            _judge.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            _judge.Dispose();
            _endedSubscription.Dispose();
        }

        [Test(Description = "벽이 파괴되면 실패를 1번 발행한다")]
        public void WallDestroyed_PublishesFail()
        {
            _wallDestroyed.Publish(new WallDestroyed());

            Assert.AreEqual(1, _ended.Count);
            Assert.AreEqual(StageOutcome.Fail, _ended[0].Outcome);
        }

        [Test(Description = "마지막 웨이브를 완료하면 클리어를 1번 발행한다")]
        public void FinalWaveCleared_PublishesClear()
        {
            _waveGaugeFilled.Publish(new WaveGaugeFilled(true));

            Assert.AreEqual(1, _ended.Count);
            Assert.AreEqual(StageOutcome.Clear, _ended[0].Outcome);
        }

        [Test(Description = "실패로 판정된 뒤에 웨이브 완료가 와도 다시 판정하지 않는다")]
        public void AfterFail_FinalWave_DoesNotPublishAgain()
        {
            _wallDestroyed.Publish(new WallDestroyed());

            _waveGaugeFilled.Publish(new WaveGaugeFilled(true));

            Assert.AreEqual(1, _ended.Count);
            Assert.AreEqual(StageOutcome.Fail, _ended[0].Outcome);
        }

        [Test(Description = "신호가 없으면 발행하지 않는다")]
        public void NothingHappened_PublishesNothing()
        {

            Assert.AreEqual(0, _ended.Count);
        }

        [Test(Description = "Dispose 뒤에는 벽이 파괴돼도 판정하지 않는다")]
        public void AfterDispose_PublishesNothing()
        {
            _judge.Dispose();

            _wallDestroyed.Publish(new WallDestroyed());
            Assert.AreEqual(0, _ended.Count);
        }

        [Test(Description = "마지막이 아닌 웨이브의 게이지가 차면 판정하지 않는다")]
        public void NonFinalWaveFilled_PublishesNothing()
        {
            _waveGaugeFilled.Publish(new WaveGaugeFilled(false));

            Assert.AreEqual(0, _ended.Count);
        }

        [Test(Description = "클리어로 판정된 뒤에 벽이 파괴돼도 다시 판정하지 않는다")]
        public void AfterClear_WallDestroyed_DoesNotPublishAgain()
        {
            _waveGaugeFilled.Publish(new WaveGaugeFilled(true));

            _wallDestroyed.Publish(new WallDestroyed());

            Assert.AreEqual(1, _ended.Count);
            Assert.AreEqual(StageOutcome.Clear, _ended[0].Outcome);
        }
    }
}
