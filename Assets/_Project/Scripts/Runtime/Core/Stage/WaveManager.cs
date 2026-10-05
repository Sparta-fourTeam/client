using System;
using System.Collections.Generic;
using Game.Core.Messages;
using MessagePipe;
using VContainer.Unity;

namespace Game.Core.Stage
{
    public sealed class WaveManager : IInitializable, IDisposable
    {
        private readonly IPublisher<WaveStarted> _waveStartedPublisher;
        private readonly ISubscriber<WaveGaugeFilled> _waveGaugeFilledSubscriber;

        private readonly IReadOnlyList<WaveDefinition> _waves; // 진행할 웨이브들의 목록 (스테이지 테이블)

        private IDisposable _subscriptions;
        private int _currentWaveIndex;

        public WaveManager(
            IPublisher<WaveStarted> waveStartedPublisher,
            ISubscriber<WaveGaugeFilled> waveGaugeFilledSubscriber,
            StageDefinition stage)
        {
            _waveStartedPublisher = waveStartedPublisher;
            _waveGaugeFilledSubscriber = waveGaugeFilledSubscriber;
            _waves = stage.Waves;
        }

        public void Initialize()
        {
            DisposableBagBuilder bag = DisposableBag.CreateBuilder();
            _waveGaugeFilledSubscriber.Subscribe(OnWaveGaugeFilled).AddTo(bag);
            _subscriptions = bag.Build();

            StartWave(_currentWaveIndex);
        }

        public void Dispose()
        {
            _subscriptions?.Dispose();
        }

        // 웨이브 게이지 찼을때
        private void OnWaveGaugeFilled(WaveGaugeFilled message)
        {
            if (message.IsFinalWave)
            {
                return;
            }

            // 웨이브 1 증가
            _currentWaveIndex++;

            if (_currentWaveIndex >= _waves.Count)
            {
                return;
            }

            StartWave(_currentWaveIndex);
        }

        // waveIndex 웨이브 시작
        private void StartWave(int waveIndex)
        {
            WaveDefinition plan = _waves[waveIndex];
            bool isFinalWave = waveIndex == _waves.Count - 1;

            _waveStartedPublisher.Publish(new WaveStarted(waveIndex + 1, plan.EnemyCount, isFinalWave, plan.MaxEliteCount, plan.MaxBossCount));
        }
    }
}
