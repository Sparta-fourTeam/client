using System;
using Game.Core.Messages;
using MessagePipe;
using VContainer.Unity;

namespace Game.Core.Stage
{
    public sealed class StageJudge : ILateTickable, IDisposable
    {
        private readonly IPublisher<StageEnded> _stageEndedPublisher;
        private readonly IDisposable _subscriptions;

        private bool _wallDestroyed;
        private bool _finalWaveCleared;
        private bool _judged;

        public StageJudge(
            ISubscriber<WallDestroyed> wallDestroyedSubscriber,
            ISubscriber<WaveGaugeFilled> waveGaugeFilledSubscriber,
            IPublisher<StageEnded> stageEndedPublisher)
        {
            _stageEndedPublisher = stageEndedPublisher;

            // 메시지를 받은 순간에는 기록만 하고, 판정은 LateTick에서 한다
            _subscriptions = DisposableBag.Create(
                wallDestroyedSubscriber.Subscribe(_ => _wallDestroyed = true),
                waveGaugeFilledSubscriber.Subscribe(OnWaveGaugeFilled));
        }

        public void LateTick()
        {
            if (_judged || (!_wallDestroyed && !_finalWaveCleared))
            {
                return;
            }

            _judged = true;

            // 같은 프레임에 둘 다 일어났으면 클리어 우선
            StageOutcome outcome = _finalWaveCleared ? StageOutcome.Clear : StageOutcome.Fail;
            _stageEndedPublisher.Publish(new StageEnded(outcome));
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
        }

        private void OnWaveGaugeFilled(WaveGaugeFilled message)
        {
            if (message.IsFinalWave)
            {
                _finalWaveCleared = true;
            }
        }
    }
}
