using System;
using System.Collections.Generic;
using Game.Core.Messages;
using MessagePipe;
using UnityEngine;
using VContainer.Unity;

namespace Game.Core
{
    public class EnemySpawner : IInitializable, IDisposable, ITickable
    {
        private readonly IEnemyFactory _enemyViewFactory;
        private readonly EnemySpawnConfig _enemySpawnConfig;
        private readonly SpawnArea _spawnArea;
        private readonly IRandomProvider _randomProvider;
        private readonly ISubscriber<WaveStarted> _waveStartedSubscriber;
        private readonly ISubscriber<WaveGaugeFilled> _waveGaugeFilledSubscriber;

        // TODO : 적 제거 (사망 / 화면 밖 이탈) 로직 붙일 때 처리 예정 
        private readonly List<EnemyModel> _activeEnemies = new();

        private IDisposable _subscriptions;
        private bool _isSpawningAllowed;

        private int _currentBurstSize; // 이번 웨이브에서 한 번에(한 버스트에) 몇 마리씩 스폰할지

        private float _elapsedTime;
        private int _spawnedCountInOnceSpawn;
        private bool _isWaitingForNextSpawn; // 다음 스폰 기다리는 상태

        private float _nextSpawnInterval;

        public EnemySpawner(
            IEnemyFactory enemyViewFactory,
            EnemySpawnConfig enemySpawnConfig,
            SpawnArea spawnArea,
            IRandomProvider randomProvider,
            ISubscriber<WaveStarted> waveStartedSubscriber,
            ISubscriber<WaveGaugeFilled> waveGaugeFilledSubscriber)
        {
            _enemyViewFactory = enemyViewFactory;
            _enemySpawnConfig = enemySpawnConfig;
            _spawnArea = spawnArea;
            _randomProvider = randomProvider;
            _waveStartedSubscriber = waveStartedSubscriber;
            _waveGaugeFilledSubscriber = waveGaugeFilledSubscriber;
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
            MoveActiveEnemies(deltaTime);

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
        }

        private void OnWaveGaugeFilled(WaveGaugeFilled message)
        {
            _isSpawningAllowed = false;
        }

        // WaveData의 타입 그대로 쓰고 위치는 SpawnArea의 현재 범위에서 매번 새로 뽑는다
        private void Spawn()
        {
            var min = _spawnArea.Min;
            var max = _spawnArea.Max;
            var spawnX = _randomProvider.Range(min.x, max.x);
            var spawnY = _randomProvider.Range(min.y, max.y);
            var spawnPosition = new Vector2(spawnX, spawnY);
            var enemy = _enemyViewFactory.Create(spawnPosition, _enemySpawnConfig.EnemyType);

            _activeEnemies.Add(enemy);
            _spawnedCountInOnceSpawn++;
        }

        private float RollSpawnInterval()
        {
            return _randomProvider.Range(_enemySpawnConfig.SpawnIntervalMin, _enemySpawnConfig.SpawnIntervalMax);
        }

        private void MoveActiveEnemies(float deltaTime)
        {
            foreach (var enemy in _activeEnemies)
            {
                enemy.Move(deltaTime);
            }
        }
    }
}