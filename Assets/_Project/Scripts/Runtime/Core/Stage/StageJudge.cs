using System;
using Game.Core.Messages;
using MessagePipe;
using VContainer.Unity;

namespace Game.Core.Stage
{
    public sealed class StageJudge : IInitializable, IDisposable
    {
        private readonly ISubscriber<WallDestroyed> _wallDestroyedSubscriber;
        private readonly ISubscriber<WaveGaugeFilled> _waveGaugeFilledSubscriber;
        private readonly IPublisher<StageEnded> _stageEndedPublisher;

        private IDisposable _subscriptions;

        private bool _judged;

        public StageJudge(
            ISubscriber<WallDestroyed> wallDestroyedSubscriber,
            ISubscriber<WaveGaugeFilled> waveGaugeFilledSubscriber,
            IPublisher<StageEnded> stageEndedPublisher)
        {
            _wallDestroyedSubscriber = wallDestroyedSubscriber;
            _waveGaugeFilledSubscriber = waveGaugeFilledSubscriber;
            _stageEndedPublisher = stageEndedPublisher;
        }

        public void Initialize()
        {
            DisposableBagBuilder bag = DisposableBag.CreateBuilder();
            _wallDestroyedSubscriber.Subscribe(_ => Judge(StageOutcome.Fail)).AddTo(bag);
            _waveGaugeFilledSubscriber.Subscribe(OnWaveGaugeFilled).AddTo(bag);
            _subscriptions = bag.Build();
        }

        public void Dispose()
        {
            _subscriptions?.Dispose();
        }

        private void OnWaveGaugeFilled(WaveGaugeFilled message)
        {
            if (message.IsFinalWave)
            {
                Judge(StageOutcome.Clear);
            }
        }

        private void Judge(StageOutcome outcome)
        {
            if (_judged)
            {
                return;
            }

            _judged = true;
            _stageEndedPublisher.Publish(new StageEnded(outcome));
        }
    }
}
