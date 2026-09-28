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
        private IPublisher<FinalWaveCleared> _finalWaveCleared;
        private List<StageEnded> _ended;
        private IDisposable _endedSubscription;
        private StageJudge _judge;

        [SetUp]
        public void Setup()
        {
            var builder = new BuiltinContainerBuilder();
            builder.AddMessagePipe();
            builder.AddMessageBroker<WallDestroyed>();
            builder.AddMessageBroker<FinalWaveCleared>();
            builder.AddMessageBroker<StageEnded>();
            IServiceProvider provider = builder.BuildServiceProvider();

            _wallDestroyed = provider.GetRequiredService<IPublisher<WallDestroyed>>();
            _finalWaveCleared = provider.GetRequiredService<IPublisher<FinalWaveCleared>>();

            _ended = new List<StageEnded>();
            _endedSubscription = provider.GetRequiredService<ISubscriber<StageEnded>>()
                .Subscribe(m => _ended.Add(m));

            _judge = new StageJudge(
                provider.GetRequiredService<ISubscriber<WallDestroyed>>(),
                provider.GetRequiredService<ISubscriber<FinalWaveCleared>>(),
                provider.GetRequiredService<IPublisher<StageEnded>>());
        }

        [TearDown]
        public void TearDown()
        {
            _judge.Dispose();
            _endedSubscription.Dispose();
        }

        [Test(Description = "벽이 파괴되면 LateTick에서 실패를 1번 발행한다")]
        public void LateTick_WallDestroyed_PublishesFail()
        {
            _wallDestroyed.Publish(new WallDestroyed());

            _judge.LateTick();

            Assert.AreEqual(1, _ended.Count);
            Assert.AreEqual(StageOutcome.Fail, _ended[0].Outcome);
        }

        [Test(Description = "마지막 웨이브를 완료하면 LateTick에서 클리어를 1번 발행한다")]
        public void LateTick_FinalWaveCleared_PublishesClear()
        {
            _finalWaveCleared.Publish(new FinalWaveCleared());

            _judge.LateTick();

            Assert.AreEqual(1, _ended.Count);
            Assert.AreEqual(StageOutcome.Clear, _ended[0].Outcome);
        }

        [Test(Description = "같은 프레임에 벽 파괴와 웨이브 완료가 모두 오면 클리어로 판정한다")]
        public void LateTick_BothInSameFrame_PublishesClear()
        {
            _wallDestroyed.Publish(new WallDestroyed());
            _finalWaveCleared.Publish(new FinalWaveCleared());

            _judge.LateTick();

            Assert.AreEqual(1, _ended.Count);
            Assert.AreEqual(StageOutcome.Clear, _ended[0].Outcome);
        }

        [Test(Description = "실패로 판정된 뒤에 웨이브 완료가 와도 다시 판정하지 않는다")]
        public void LateTick_AfterJudged_DoesNotPublishAgain()
        {
            _wallDestroyed.Publish(new WallDestroyed());
            _judge.LateTick();

            _finalWaveCleared.Publish(new FinalWaveCleared());
            _judge.LateTick();

            Assert.AreEqual(1, _ended.Count);
            Assert.AreEqual(StageOutcome.Fail, _ended[0].Outcome);
        }

        [Test(Description = "아무 일도 없으면 LateTick이 불려도 발행하지 않는다")]
        public void LateTick_NothingHappened_PublishesNothing()
        {
            _judge.LateTick();
            _judge.LateTick();

            Assert.AreEqual(0, _ended.Count);
        }

        [Test(Description = "LateTick 전에는 신호를 받아도 발행하지 않는다")]
        public void BeforeLateTick_PublishesNothing()
        {
            _wallDestroyed.Publish(new WallDestroyed());

            Assert.AreEqual(0, _ended.Count);
        }

        [Test(Description = "Dispose 뒤에는 벽이 파괴돼도 판정하지 않는다")]
        public void LateTick_AfterDispose_PublishesNothing()
        {
            _judge.Dispose();

            _wallDestroyed.Publish(new WallDestroyed());
            _judge.LateTick();

            Assert.AreEqual(0, _ended.Count);
        }
    }
}
