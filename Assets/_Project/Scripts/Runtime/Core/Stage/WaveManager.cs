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

        private readonly List<WavePlan> _waves; // 진행할 웨이브들의 목록

        private IDisposable _subscriptions;
        private int _currentWaveIndex;

        public WaveManager(
            IPublisher<WaveStarted> waveStartedPublisher,
            ISubscriber<WaveGaugeFilled> waveGaugeFilledSubscriber)
        {
            _waveStartedPublisher = waveStartedPublisher;
            _waveGaugeFilledSubscriber = waveGaugeFilledSubscriber;
            _waves = CreateWavePlans();
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
            WavePlan plan = _waves[waveIndex];
            bool isFinalWave = waveIndex == _waves.Count - 1;

            _waveStartedPublisher.Publish(new WaveStarted(waveIndex + 1, plan.EnemyCount, isFinalWave, plan.MaxEliteCount, plan.MaxBossCount));
        }

        // TODO : 현재는 하드코딩, 추후 데이터 연동시 수정할 수 있음.
        private static List<WavePlan> CreateWavePlans()
        {
            return new List<WavePlan>
            {
                new WavePlan(enemyCount: 5),
                new WavePlan(enemyCount: 8,  maxEliteCount: 2),
                new WavePlan(enemyCount: 10, maxBossCount: 1),
            };
        }

        private readonly struct WavePlan
        {
            public int EnemyCount { get; }
            public int MaxEliteCount { get; }
            public int MaxBossCount { get; }

            public WavePlan(int enemyCount, int maxEliteCount = 0, int maxBossCount = 0)
            {
                EnemyCount = enemyCount;
                MaxEliteCount = maxEliteCount;
                MaxBossCount = maxBossCount;
            }
        }
    }
}