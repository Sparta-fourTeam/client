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
        private readonly IPublisher<WaveCompleted> _waveCompletedPublisher;

        private readonly Queue<WaveStarted> _waves = new Queue<WaveStarted>();

        private IDisposable _subscriptions;
        private int _kills;

        public WaveProgress(
            ISubscriber<WaveStarted> waveStartedSubscriber,
            ISubscriber<EnemyDied> enemyDiedSubscriber,
            IBufferedPublisher<WaveGaugeChanged> gaugeChangedPublisher,
            IPublisher<WaveCompleted> waveCompletedPublisher)
        {
            _waveStartedSubscriber = waveStartedSubscriber;
            _enemyDiedSubscriber = enemyDiedSubscriber;
            _gaugeChangedPublisher = gaugeChangedPublisher;
            _waveCompletedPublisher = waveCompletedPublisher;
        }

        public void Initialize()
        {
            DisposableBagBuilder bag = DisposableBag.CreateBuilder();
            _waveStartedSubscriber.Subscribe(OnWaveStarted).AddTo(bag);
            // 분열·소환으로 생긴 적의 사망도 센다. 총량(EnemyCount)에는 들어 있지 않으므로 그만큼 웨이브가 일찍 끝난다
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
            _waveCompletedPublisher.Publish(new WaveCompleted(current.WaveIndex, current.IsFinalWave));

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
