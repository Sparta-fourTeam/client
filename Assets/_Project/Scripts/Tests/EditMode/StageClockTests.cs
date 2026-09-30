using System;
using Game.Core;
using Game.Core.Messages;
using MessagePipe;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class StageClockTests
    {
        private IPublisher<StageStateChanged> _stateChanged;
        private StageClock _clock;

        [SetUp]
        public void SetUp()
        {
            var builder = new BuiltinContainerBuilder();
            builder.AddMessagePipe();
            builder.AddMessageBroker<StageStateChanged>();
            IServiceProvider provider = builder.BuildServiceProvider();

            _stateChanged = provider.GetRequiredService<IPublisher<StageStateChanged>>();
            _clock = new StageClock(provider.GetRequiredService<ISubscriber<StageStateChanged>>());
            _clock.Start();
        }

        [TearDown]
        public void TearDown()
        {
            _clock.Dispose();
        }

        private void Enter(StageState state)
        {
            _stateChanged.Publish(new StageStateChanged(state));
        }

        [Test(Description = "Playing 상태가 되기 전에는 시간이 쌓이지 않는다")]
        public void BeforePlaying_DoesNotAccumulate()
        {
            _clock.Accumulate(1f);

            Assert.AreEqual(0f, _clock.ElapsedSeconds);
        }

        [Test(Description = "Playing 상태에서는 흐른 시간만큼 쌓인다")]
        public void WhilePlaying_Accumulates()
        {
            Enter(StageState.Playing);

            _clock.Accumulate(1.5f);
            _clock.Accumulate(0.5f);

            Assert.AreEqual(2f, _clock.ElapsedSeconds, 0.0001f);
        }

        [Description("Playing이 아닌 상태에서는 시간이 멈춘다")]
        [TestCase(StageState.Paused)]
        [TestCase(StageState.CardSelect)]
        [TestCase(StageState.Submitting)]
        [TestCase(StageState.Finished)]
        public void WhenNotPlaying_StopsAccumulating(StageState state)
        {
            Enter(StageState.Playing);
            _clock.Accumulate(1f);

            Enter(state);
            _clock.Accumulate(5f);

            Assert.AreEqual(1f, _clock.ElapsedSeconds, 0.0001f);
        }

        [Test(Description = "멈췄다가 Playing으로 돌아오면 다시 쌓이고 이전 기록은 유지된다")]
        public void ResumePlaying_ContinuesFromPreviousTime()
        {
            Enter(StageState.Playing);
            _clock.Accumulate(1f);
            Enter(StageState.Paused);
            _clock.Accumulate(5f);
            Enter(StageState.Playing);
            _clock.Accumulate(2f);

            Assert.AreEqual(3f, _clock.ElapsedSeconds, 0.0001f);
        }

        [Test(Description = "Dispose 뒤에는 상태 변경을 받지 않는다")]
        public void AfterDispose_IgnoresStateChanges()
        {
            _clock.Dispose();

            Enter(StageState.Playing);
            _clock.Accumulate(1f);

            Assert.AreEqual(0f, _clock.ElapsedSeconds);
        }
    }
}
