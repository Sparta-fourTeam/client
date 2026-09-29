using System;
using Game.Core.Messages;
using MessagePipe;
using UnityEngine;
using VContainer.Unity;

namespace Game.Core
{
    public sealed class BattleStats : IInitializable, ITickable, IDisposable
    {
        private readonly ISubscriber<EnemyDied> _enemyDiedSubscriber;
        private readonly ISubscriber<WaveGaugeChanged> _waveGaugeChangedSubscriber;
        private readonly ISubscriber<StageEnded> _stageEndedSubscriber;

        private IDisposable _subscriptions;

        public int Kills { get; private set; }
        public int ReachedWave { get; private set; }
        public float PlayTime { get; private set; }
        public bool IsEnded { get; private set; }

        public BattleStats(
            ISubscriber<EnemyDied> enemyDiedSubscriber,
            ISubscriber<WaveGaugeChanged> waveGaugeChangedSubscriber,
            ISubscriber<StageEnded> stageEndedSubscriber)
        {
            _enemyDiedSubscriber = enemyDiedSubscriber;
            _waveGaugeChangedSubscriber = waveGaugeChangedSubscriber;
            _stageEndedSubscriber = stageEndedSubscriber;
        }

        public void Initialize()
        {
            DisposableBagBuilder bag = DisposableBag.CreateBuilder();
            _enemyDiedSubscriber.Subscribe(_ => OnEnemyDied()).AddTo(bag);
            _waveGaugeChangedSubscriber.Subscribe(OnWaveGaugeChanged).AddTo(bag);
            _stageEndedSubscriber.Subscribe(_ => IsEnded = true).AddTo(bag);
            _subscriptions = bag.Build();
        }

        // 일시정지(timeScale 0) 중에는 deltaTime이 0이라 시간이 멈춘다
        public void Tick()
        {
            Advance(Time.deltaTime);
        }

        public void Advance(float deltaTime)
        {
            if (IsEnded || deltaTime <= 0f)
            {
                return;
            }

            PlayTime += deltaTime;
        }

        public void Dispose()
        {
            _subscriptions?.Dispose();
        }

        private void OnEnemyDied()
        {
            if (IsEnded)
            {
                return;
            }

            Kills++;
        }

        // HUD와 같은 기준: 지금 게이지가 채우고 있는 웨이브
        private void OnWaveGaugeChanged(WaveGaugeChanged message)
        {
            if (IsEnded)
            {
                return;
            }

            ReachedWave = Math.Max(ReachedWave, message.WaveIndex);
        }
    }
}
