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
        private readonly IPublisher<AllEnemiesCleared> _allEnemiesClearedPublisher;

        // 필드에 있는 적
        // 죽은 적은 다음 Tick에 제거, EnemyDied는 EnemyModel이 발행
        private readonly List<Enemy> _activeEnemies = new();

        // 분열·소환 요청. 죽는 순간(스킬 처리나 TickCombat 안)에는 목록을 건드리지 않고 다음 Advance 맨 앞에서 만든다
        private readonly Queue<EnemySpawnRequest> _pendingSpawns = new();
        private readonly Wall _wall;
        private readonly EnemyProjectileSystem _projectiles;

        private IDisposable _subscriptions;
        private bool _isSpawningAllowed;

        // 지금 버스트에서 스폰할 몬스터 ID를 섞어 둔 순서. 하나씩 꺼내 스폰한다
        private readonly List<int> _burst = new();
        // 웨이브 구성. 첫 버스트는 전부, 이후 반복 버스트는 일반 몬스터만 쓴다 (엘리트·보스는 웨이브당 한 번)
        private IReadOnlyList<WaveSpawn> _waveSpawns = Array.Empty<WaveSpawn>();
        private int _currentBurstSize; // 지금 버스트의 스폰 수

        private float _elapsedTime;
        private int _spawnedCountInOnceSpawn;
        private bool _isWaitingForNextSpawn; // 다음 스폰 기다리는 상태

        private float _nextSpawnInterval;

        private bool _isFinalWave;
        private bool _finalSpawnDone;
        private bool _allClearPublished;

        public EnemySpawner(
            IEnemyFactory enemyViewFactory,
            EnemySpawnConfig enemySpawnConfig,
            SpawnArea spawnArea,
            IRandomProvider randomProvider,
            ISubscriber<WaveStarted> waveStartedSubscriber,
            IPublisher<AllEnemiesCleared> allEnemiesClearedPublisher,
            Wall wall,
            EnemyProjectileSystem projectiles)
        {
            _enemyViewFactory = enemyViewFactory;
            _enemySpawnConfig = enemySpawnConfig;
            _spawnArea = spawnArea;
            _randomProvider = randomProvider;
            _waveStartedSubscriber = waveStartedSubscriber;
            _allEnemiesClearedPublisher = allEnemiesClearedPublisher;
            _wall = wall;
            _projectiles = projectiles;
            _nextSpawnInterval = RollSpawnInterval();
        }

        public void Initialize()
        {
            DisposableBagBuilder bag = DisposableBag.CreateBuilder();
            _waveStartedSubscriber.Subscribe(OnWaveStarted).AddTo(bag);
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
            FlushSpawnRequests();
            TickCombat(deltaTime);

            if (deltaTime > 0f)
            {
                CheckAllEnemiesCleared();
            }

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
                    _isWaitingForNextSpawn = false;
                    // 이미 나온 엘리트·보스는 다시 내지 않는다. 일반 몬스터가 없는 웨이브는 더 낼 것이 없다
                    if (!StartBurst(includeSpecials: false))
                    {
                        _isSpawningAllowed = false;
                    }
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
                if (_isFinalWave)
                {
                    _isSpawningAllowed = false;
                    _finalSpawnDone = true;
                }
                else
                {
                    _isWaitingForNextSpawn = true;
                }
            }
        }

        private void OnWaveStarted(WaveStarted message)
        {
            _waveSpawns = message.Spawns;
            _isSpawningAllowed = StartBurst(includeSpecials: true);
            _elapsedTime = 0f;
            _isWaitingForNextSpawn = false;
            _isFinalWave = message.IsFinalWave;
        }

        private void CheckAllEnemiesCleared()
        {
            // 아직 만들지 않은 분열체가 있으면 클리어가 아니다
            if (!_finalSpawnDone || _allClearPublished || _pendingSpawns.Count > 0)
            {
                return;
            }

            foreach (var enemy in _activeEnemies)
            {
                if (!enemy.IsDead)
                {
                    return;
                }
            }

            _allClearPublished = true;
            _allEnemiesClearedPublisher.Publish(new AllEnemiesCleared());
        }

        /// <summary>웨이브 구성으로 새 버스트를 만든다. 낼 몬스터가 없으면 false</summary>
        private bool StartBurst(bool includeSpecials)
        {
            _burst.Clear();
            foreach (var spawn in _waveSpawns)
            {
                if (spawn.Type != EnemyType.Normal && !includeSpecials) { continue; }
                for (int i = 0; i < spawn.Count; i++) { _burst.Add(spawn.MonsterId); }
            }

            // 구성 순서대로 내면 같은 몬스터가 몰려 나오므로 섞는다
            for (int i = _burst.Count - 1; i > 0; i--)
            {
                int j = Mathf.Clamp((int)_randomProvider.Range(0f, i + 1), 0, i);
                (_burst[i], _burst[j]) = (_burst[j], _burst[i]);
            }

            _currentBurstSize = _burst.Count;
            _spawnedCountInOnceSpawn = 0;
            return _burst.Count > 0;
        }

        // 버스트에서 몬스터 하나를 꺼내 스폰한다. 위치는 SpawnArea의 현재 범위에서 매번 새로 뽑는다
        private void Spawn()
        {
            var min = _spawnArea.Min;
            var max = _spawnArea.Max;
            var spawnX = _randomProvider.Range(min.x, max.x);
            var spawnY = _randomProvider.Range(min.y, max.y);
            var spawnPosition = new Vector2(spawnX, spawnY);
            var enemy = _enemyViewFactory.Create(_burst[_spawnedCountInOnceSpawn], spawnPosition);

            Track(enemy);
            _spawnedCountInOnceSpawn++;
        }

        private void Track(Enemy enemy)
        {
            _activeEnemies.Add(enemy);
            enemy.SpawnRequested += OnSpawnRequested;
        }

        private void OnSpawnRequested(EnemySpawnRequest request)
        {
            _pendingSpawns.Enqueue(request);
        }

        private void FlushSpawnRequests()
        {
            while (_pendingSpawns.Count > 0)
            {
                var request = _pendingSpawns.Dequeue();
                Track(_enemyViewFactory.Create(request.MonsterId, request.Position, isSummoned: true));
            }
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
                    enemy.SpawnRequested -= OnSpawnRequested;
                    _activeEnemies.RemoveAt(i);
                    continue;
                }

                // 사거리 안이면 공격, 밖이면 벽 쪽으로 이동, 상태이상과 패시브 시간 진행
                enemy.Tick(deltaTime, _wall, _projectiles);
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

            // 투사체가 매 프레임 부르므로 전체 정렬 대신 가까운 count개만 삽입 정렬로 유지한다 (할당 없음)
            foreach (var enemy in _activeEnemies)
            {
                if (enemy.IsDead)
                {
                    continue;
                }

                float distance = (enemy.Position - from).sqrMagnitude;
                int index;
                if (results.Count < count)
                {
                    results.Add(enemy);
                    index = results.Count - 1;
                }
                else if (distance < (results[count - 1].Position - from).sqrMagnitude)
                {
                    results[count - 1] = enemy;
                    index = count - 1;
                }
                else
                {
                    continue;
                }

                while (index > 0 && (results[index - 1].Position - from).sqrMagnitude > distance)
                {
                    (results[index - 1], results[index]) = (results[index], results[index - 1]);
                    index--;
                }
            }

            return results.Count;
        }
    }
}
