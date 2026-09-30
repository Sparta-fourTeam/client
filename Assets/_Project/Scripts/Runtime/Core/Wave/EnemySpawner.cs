using System;
using System.Collections.Generic;
using Game.Core.Defense;
using Game.Core.Messages;
using MessagePipe;
using UnityEngine;
using VContainer.Unity;

namespace Game.Core
{
    public class EnemySpawner : IInitializable, IDisposable, ITickable, IEnemyTargetProvider
    {
        private readonly IEnemyFactory _enemyViewFactory;
        private readonly EnemySpawnConfig _enemySpawnConfig;
        private readonly SpawnArea _spawnArea;
        private readonly IRandomProvider _randomProvider;
        private readonly ISubscriber<WaveStarted> _waveStartedSubscriber;
        private readonly ISubscriber<WaveGaugeFilled> _waveGaugeFilledSubscriber;

        // 필드에 있는 적
        // 죽은 적은 다음 Tick에 제거, EnemyDied는 EnemyModel이 발행
        private readonly List<EnemyModel> _activeEnemies = new();
        private readonly Wall _wall;
        private readonly EnemyProjectileSystem _projectiles;

        private IDisposable _subscriptions;
        private bool _isSpawningAllowed;

        private int _currentBurstSize; // 이번 웨이브에서 한 번에(한 버스트에) 몇 마리씩 스폰할지

        private float _elapsedTime;
        private int _spawnedCountInOnceSpawn;
        private bool _isWaitingForNextSpawn; // 다음 스폰 기다리는 상태

        private float _nextSpawnInterval;
        private int _remainingElite; // 남은 엘리트 몬스터 수
        private int _remainingBoss; // 남은 보스 몬스터 수

        public EnemySpawner(
            IEnemyFactory enemyViewFactory,
            EnemySpawnConfig enemySpawnConfig,
            SpawnArea spawnArea,
            IRandomProvider randomProvider,
            ISubscriber<WaveStarted> waveStartedSubscriber,
            ISubscriber<WaveGaugeFilled> waveGaugeFilledSubscriber,
            Wall wall,
            EnemyProjectileSystem projectiles)
        {
            _enemyViewFactory = enemyViewFactory;
            _enemySpawnConfig = enemySpawnConfig;
            _spawnArea = spawnArea;
            _randomProvider = randomProvider;
            _waveStartedSubscriber = waveStartedSubscriber;
            _waveGaugeFilledSubscriber = waveGaugeFilledSubscriber;
            _wall = wall;
            _projectiles = projectiles;
            _nextSpawnInterval = RollSpawnInterval();
        }

        public void Initialize()
        {
            DisposableBagBuilder bag = DisposableBag.CreateBuilder();
            _waveStartedSubscriber.Subscribe(OnWaveStarted).AddTo(bag);
            _waveGaugeFilledSubscriber.Subscribe(OnWaveGaugeFilled).AddTo(bag);
            _subscriptions = bag.Build();
        }

        public void Dispose()
        {
            _subscriptions?.Dispose();
        }

        public void Tick()
        {
            Advance(Time.deltaTime);
        }

        // 스폰 로직
        public void Advance(float deltaTime)
        {
            TickCombat(deltaTime);

            if (!_isSpawningAllowed)
            {
                return;
            }

            _elapsedTime += deltaTime;

            if (_isWaitingForNextSpawn)
            {
                if (_elapsedTime >= _enemySpawnConfig.SpawnCooldown)
                {
                    _elapsedTime = 0f;
                    _spawnedCountInOnceSpawn = 0;
                    _isWaitingForNextSpawn = false;
                }
                return;
            }

            // 스폰 간격 아직 안 지났으니 대기
            if (_elapsedTime < _nextSpawnInterval)
            {
                return;
            }

            _elapsedTime = 0f;
            Spawn();

            // 스폰 간격 랜덤
            _nextSpawnInterval = RollSpawnInterval();

            // 스폰 수량 채웠으면 대기 상태 전환
            if (_spawnedCountInOnceSpawn >= _currentBurstSize)
            {
                _isWaitingForNextSpawn = true;
            }
        }

        private void OnWaveStarted(WaveStarted message)
        {
            _isSpawningAllowed = true;
            _currentBurstSize = message.EnemyCount;
            _elapsedTime = 0f;
            _spawnedCountInOnceSpawn = 0;
            _isWaitingForNextSpawn = false;
            _remainingElite = message.MaxEliteCount;
            _remainingBoss = message.MaxBossCount;
        }

        private void OnWaveGaugeFilled(WaveGaugeFilled message)
        {
            _isSpawningAllowed = false;
        }

        private EnemyType PickEnemyType()
        {
            if(_remainingBoss > 0)
            {
                _remainingBoss--;
                return EnemyType.Boss;
            }

            if(_remainingElite > 0 && _randomProvider.Range(0f, 1f) < _enemySpawnConfig.EliteSpawnChance)
            {
                _remainingElite--;
                return EnemyType.Elite;
            }

            return EnemyType.Normal;
        }


        // PickEnemyType으로 타입을 고르고 위치는 SpawnArea의 현재 범위에서 매번 새로 뽑는다
        private void Spawn()
        {
            var min = _spawnArea.Min;
            var max = _spawnArea.Max;
            var spawnX = _randomProvider.Range(min.x, max.x);
            var spawnY = _randomProvider.Range(min.y, max.y);
            var spawnPosition = new Vector2(spawnX, spawnY);
            var enemy = _enemyViewFactory.Create(spawnPosition, PickEnemyType());

            _activeEnemies.Add(enemy);
            _spawnedCountInOnceSpawn++;
        }

        private float RollSpawnInterval()
        {
            return _randomProvider.Range(_enemySpawnConfig.SpawnIntervalMin, _enemySpawnConfig.SpawnIntervalMax);
        }

        // 적 움직임, 공격 로직
        private void TickCombat(float deltaTime)
        {
            for (int i = _activeEnemies.Count - 1; i >= 0; i--)
            {
                var enemy = _activeEnemies[i];
                if (enemy.IsDead)
                {
                    _activeEnemies.RemoveAt(i);
                    continue;
                }

                // 사거리 안이면 공격
                // 밖이면 벽 쪽으로 이동
                if (enemy.IsInAttackRange(_wall))
                {
                    enemy.Attack(deltaTime, _wall, _projectiles);
                }
                else
                {
                    enemy.Move(deltaTime);
                }
            }

            // 투사체 처리
            _projectiles.Tick(deltaTime, _wall);
        }

        public int GetNearest(Vector2 from, int count, List<IEnemyTarget> results)
        {
            results.Clear();
            if (count <= 0)
            {
                return 0;
            }

            foreach(var enemy in _activeEnemies)
            {
                if(!enemy.IsDead)
                {
                    results.Add(enemy);
                }
            }

            results.Sort((a, b) => (a.Position - from).sqrMagnitude.CompareTo((b.Position - from).sqrMagnitude));

            if (results.Count > count)
            {
                results.RemoveRange(count, results.Count - count);
            }

            return results.Count;
        }
    }
}