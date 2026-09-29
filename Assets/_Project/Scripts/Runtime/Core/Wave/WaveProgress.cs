using System;
using System.Collections.Generic;
using Game.Core.Messages;
using MessagePipe;
using VContainer.Unity;

namespace Game.Core
{
    public sealed class WaveProgress : IInitializable, IDisposable
    {
        private readonly ISubscriber<WaveStarted> _waveStartedSubscriber;
        private readonly ISubscriber<EnemyDied> _enemyDiedSubscriber;
        private readonly IBufferedPublisher<WaveGaugeChanged> _gaugeChangedPublisher;
        private readonly IPublisher<WaveGaugeFilled> _gaugeFilledPublisher;

        private readonly Queue<WaveStarted> _waves = new Queue<WaveStarted>();

        private IDisposable _subscriptions;
        private int _kills;

        public WaveProgress(
            ISubscriber<WaveStarted> waveStartedSubscriber,
            ISubscriber<EnemyDied> enemyDiedSubscriber,
            IBufferedPublisher<WaveGaugeChanged> gaugeChangedPublisher,
            IPublisher<WaveGaugeFilled> gaugeFilledPublisher)
        {
            _waveStartedSubscriber = waveStartedSubscriber;
            _enemyDiedSubscriber = enemyDiedSubscriber;
            _gaugeChangedPublisher = gaugeChangedPublisher;
            _gaugeFilledPublisher = gaugeFilledPublisher;
        }

        public void Initialize()
        {
            DisposableBagBuilder bag = DisposableBag.CreateBuilder();
            _waveStartedSubscriber.Subscribe(OnWaveStarted).AddTo(bag);
            _enemyDiedSubscriber.Subscribe(_ => OnEnemyDied()).AddTo(bag);
            _subscriptions = bag.Build();
        }

        public void Dispose()
        {
            _subscriptions?.Dispose();
        }

        private void OnWaveStarted(WaveStarted wave)
        {
            // 몬스터가 없는 웨이브는 무시
            if (wave.EnemyCount <= 0)
            {
                return;
            }

            _waves.Enqueue(wave);

            // 채우던 웨이브가 없었다면 이 웨이브부터 채우기 시작
            if (_waves.Count == 1)
            {
                PublishGauge();
            }
        }

        private void OnEnemyDied()
        {
            // 시작된 웨이브가 없으면 셀 게이지도 없다
            if (_waves.Count == 0)
            {
                return;
            }

            _kills++;
            PublishGauge();

            WaveStarted current = _waves.Peek();
            if (_kills < current.EnemyCount)
            {
                return;
            }

            _waves.Dequeue();
            _kills = 0;
            _gaugeFilledPublisher.Publish(new WaveGaugeFilled(current.IsFinalWave));

            // 이미 시작된 다음 웨이브가 있으면 그 크기로 0부터 다시
            if (_waves.Count > 0)
            {
                PublishGauge();
            }
        }

        private void PublishGauge()
        {
            WaveStarted current = _waves.Peek();
            _gaugeChangedPublisher.Publish(new WaveGaugeChanged(current.WaveIndex, _kills, _waves.Peek().EnemyCount));
        }
    }
}
