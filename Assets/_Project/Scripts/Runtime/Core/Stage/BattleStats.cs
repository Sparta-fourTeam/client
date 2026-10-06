using System;
using System.Collections.Generic;
using Game.Core.Messages;
using MessagePipe;
using UnityEngine;
using VContainer.Unity;

namespace Game.Core
{
    public sealed class BattleStats : IInitializable, ITickable, IDisposable
    {
        private readonly ISubscriber<EnemyDied> _enemyDiedSubscriber;
        private readonly ISubscriber<WaveGaugeChanged> _waveGaugeChangedSubscriber; // todo 웨이브 게이지 채워질때마다 판단하디말고 다 채워졌을때
        private readonly ISubscriber<StageEnded> _stageEndedSubscriber;
        private readonly ISubscriber<WallHpChanged> _wallHpChanged;

        private IDisposable _subscriptions;
        private readonly HashSet<int> _deadEnemies = new();

        public int Kills { get; private set; }
        public int ReachedWave { get; private set; }
        public float PlayTime { get; private set; }
        public bool IsEnded { get; private set; }
        public int WallHpPercent { get; private set; } = 100;
        public List<string> BuildLog { get; } = new();

        public BattleStats(
            ISubscriber<EnemyDied> enemyDiedSubscriber,
            ISubscriber<WaveGaugeChanged> waveGaugeChangedSubscriber,
            ISubscriber<StageEnded> stageEndedSubscriber,
            ISubscriber<WallHpChanged> wallHpChanged)
        {
            _enemyDiedSubscriber = enemyDiedSubscriber;
            _waveGaugeChangedSubscriber = waveGaugeChangedSubscriber;
            _stageEndedSubscriber = stageEndedSubscriber;
            _wallHpChanged = wallHpChanged;
        }

        public void Initialize()
        {
            DisposableBagBuilder bag = DisposableBag.CreateBuilder();
            _enemyDiedSubscriber.Subscribe(OnEnemyDied).AddTo(bag);
            _waveGaugeChangedSubscriber.Subscribe(OnWaveGaugeChanged).AddTo(bag);
            _stageEndedSubscriber.Subscribe(_ => IsEnded = true).AddTo(bag);
            _wallHpChanged.Subscribe(e => WallHpPercent = e.Max <= 0 ? 0 : e.Current * 100 / e.Max).AddTo(bag);
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

        public void RecordCard(string cardId)
        {
            BuildLog.Add(cardId);
        }

        public void Dispose()
        {
            _subscriptions?.Dispose();
        }

        private void OnEnemyDied(EnemyDied message)
        {
            if (IsEnded)
            {
                return;
            }

            if (message.EnemyId > 0 && !_deadEnemies.Add(message.EnemyId)) { return; }
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
