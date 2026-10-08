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
        private readonly ISubscriber<WaveCompleted> _waveCompletedSubscriber;

        private readonly IReadOnlyList<WaveDefinition> _waves; // 진행할 웨이브들의 목록 (스테이지 테이블)

        private IDisposable _subscriptions;
        private int _currentWaveIndex;

        public WaveManager(
            IPublisher<WaveStarted> waveStartedPublisher,
            ISubscriber<WaveCompleted> waveCompletedSubscriber,
            StageDefinition stage)
        {
            _waveStartedPublisher = waveStartedPublisher;
            _waveCompletedSubscriber = waveCompletedSubscriber;
            _waves = stage.Waves;
        }

        public void Initialize()
        {
            DisposableBagBuilder bag = DisposableBag.CreateBuilder();
            _waveCompletedSubscriber.Subscribe(OnWaveCompleted).AddTo(bag);
            _subscriptions = bag.Build();

            StartWave(_currentWaveIndex);
        }

        public void Dispose()
        {
            _subscriptions?.Dispose();
        }

        // 현재 웨이브 완료 시 다음 웨이브를 시작한다.
        private void OnWaveCompleted(WaveCompleted message)
        {
            if (message.IsFinalWave || message.WaveIndex != _currentWaveIndex + 1)
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

            _waveStartedPublisher.Publish(new WaveStarted(waveIndex + 1, isFinalWave, plan.Spawns));
        }
    }
}
