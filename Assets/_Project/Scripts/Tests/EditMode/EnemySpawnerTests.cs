using System;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Defense;
using Game.Core.Messages;
using MessagePipe;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Tests
{
    public class EnemySpawnerTests
    {
        // ───────── Fakes ─────────

        // Wall.Construct용 (IBufferedPublisher 겸용)
        private class FakeWallPublisher<T> : IPublisher<T>, IBufferedPublisher<T>
        {
            public void Publish(T message) { }
        }

        private class FakePublisher<T> : IPublisher<T>
        {
            public readonly List<T> Published = new();
            public void Publish(T message) => Published.Add(message);
        }

        private class FakeEnemyViewFactory : IEnemyFactory
        {
            private readonly float _speed;
            private readonly int _maxHp;
            private readonly EnemyAttackStats _attack;
            private int _nextId;

            public int CreateCallCount;
            public EnemyModel LastCreatedEnemy;
            public readonly List<EnemyModel> Created = new();
            public readonly FakePublisher<EnemyHpChanged> HpChanged = new();
            public readonly FakePublisher<EnemyDied> Died = new();

            // 채워두면 스폰 위치 대신 이 위치를 순서대로 사용 (GetNearest 테스트용)
            public readonly Queue<Vector2> PositionOverrides = new();

            public FakeEnemyViewFactory(float speed, int maxHp = 10, EnemyAttackStats? attack = null)
            {
                _speed = speed;
                _maxHp = maxHp;
                _attack = attack ?? TestAttack;
            }

            public EnemyModel Create(Vector2 spawnPosition, EnemyType type)
            {
                var position = PositionOverrides.Count > 0 ? PositionOverrides.Dequeue() : spawnPosition;

                CreateCallCount++;
                LastCreatedEnemy = new EnemyModel(++_nextId, position, _speed, type, _maxHp, _attack, HpChanged, Died);
                Created.Add(LastCreatedEnemy);

                return LastCreatedEnemy;
            }

            public EnemyModel CreateByMonsterId(int monsterId, Vector2 spawnPosition)
            {
                CreateCallCount++;
                LastCreatedEnemy = new EnemyModel(++_nextId, spawnPosition, _speed, EnemyType.Normal, _maxHp, _attack,
                    HpChanged, Died, null, true);
                Created.Add(LastCreatedEnemy);

                return LastCreatedEnemy;
            }
        }

        private class FakeRandomProvider : IRandomProvider
        {
            private readonly float _fixedValue;

            // Range가 어떤 min/max로 호출됐는지 순서대로 기록
            public readonly List<(float Min, float Max)> Calls = new();

            public FakeRandomProvider(float fixedValue) => _fixedValue = fixedValue;

            public float Range(float min, float max)
            {
                Calls.Add((min, max));
                return _fixedValue; // 항상 이 값만 반환
            }
        }

        // 테스트에서 WaveStarted를 직접 발행할 수 있게 해주는 가짜 구독자
        private class FakeSubscriber<T> : ISubscriber<T>
        {
            private readonly List<IMessageHandler<T>> _handlers = new();

            public IDisposable Subscribe(IMessageHandler<T> handler, params MessageHandlerFilter<T>[] filters)
            {
                _handlers.Add(handler);
                return new Subscription(_handlers, handler);
            }

            public void Publish(T message)
            {
                foreach (var handler in _handlers.ToArray())
                {
                    handler.Handle(message);
                }
            }

            private class Subscription : IDisposable
            {
                private readonly List<IMessageHandler<T>> _handlers;
                private readonly IMessageHandler<T> _handler;

                public Subscription(List<IMessageHandler<T>> handlers, IMessageHandler<T> handler)
                {
                    _handlers = handlers;
                    _handler = handler;
                }

                public void Dispose() => _handlers.Remove(_handler);
            }
        }

        // ───────── Setup / Helpers ─────────

        private const int WallMaxHp = 100;
        private const float FarWallY = -100f; // 스폰·이동 테스트용: 적이 사거리에 들어가지 않게 멀리

        private static readonly EnemyAttackStats TestAttack = new EnemyAttackStats(AttackType.Melee, 10, 1f, 0f);

        private readonly List<GameObject> _createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _createdObjects)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }
            _createdObjects.Clear();
        }

        private Wall CreateTestWall(float lineY = FarWallY, int maxHp = WallMaxHp)
        {
            var go = new GameObject("Wall");
            _createdObjects.Add(go);
            go.transform.position = new Vector3(0f, lineY, 0f); // _attackLineOffset 기본값 0 → AttackLineY == lineY

            var wall = go.AddComponent<Wall>();
            wall.Construct(new FakeWallPublisher<WallHpChanged>(), new FakeWallPublisher<WallDestroyed>(), null);
            wall.Initialize(maxHp); // EditMode에서는 Start가 호출되지 않음
            return wall;
        }

        private SpawnArea CreateTestSpawnArea()
        {
            var go = new GameObject("SpawnArea");
            _createdObjects.Add(go);
            return go.AddComponent<SpawnArea>();
        }

        private static EnemySpawnConfig CreateTestWaveData()
        {
            return new EnemySpawnConfig(
                spawnIntervalMin: 0.1f,
                spawnIntervalMax: 0.5f,
                spawnCooldown: 3f);
        }

        // 스포너와 무관하게 쓰는 더미 적
        private static EnemyModel CreateStandaloneEnemy()
        {
            return new EnemyModel(0, Vector2.zero, 0f, EnemyType.Normal, 10, TestAttack,
                new FakePublisher<EnemyHpChanged>(), new FakePublisher<EnemyDied>());
        }

        private EnemySpawner CreateSpawner(
            FakeEnemyViewFactory factory,
            out FakeSubscriber<WaveStarted> waveStarted,
            out FakePublisher<AllEnemiesCleared> allCleared,
            Wall wall = null,
            EnemyProjectileSystem projectiles = null,
            SpawnArea spawnArea = null,
            FakeRandomProvider random = null)
        {
            waveStarted = new FakeSubscriber<WaveStarted>();
            allCleared = new FakePublisher<AllEnemiesCleared>();

            var spawner = new EnemySpawner(
                factory,
                CreateTestWaveData(),
                spawnArea ?? CreateTestSpawnArea(),
                random ?? new FakeRandomProvider(1f),
                waveStarted,
                allCleared,
                wall ?? CreateTestWall(),
                projectiles ?? new EnemyProjectileSystem());
            spawner.Initialize();
            return spawner;
        }

        private static void StartWave(FakeSubscriber<WaveStarted> waveStarted, int enemyCount, bool isFinal = false)
        {
            waveStarted.Publish(new WaveStarted(waveIndex: 1, enemyCount: enemyCount, isFinalWave: isFinal));
        }

        // 지정한 위치에 적을 스폰해 둔 스포너. 속도 0이라 적이 움직이지 않음
        private EnemySpawner CreateSpawnerWithEnemies(out FakeEnemyViewFactory factory, params Vector2[] positions)
        {
            factory = new FakeEnemyViewFactory(0f);
            foreach (var position in positions)
            {
                factory.PositionOverrides.Enqueue(position);
            }

            var spawner = CreateSpawner(factory, out var waveStarted, out _);
            StartWave(waveStarted, enemyCount: 10); // 버스트를 넉넉히 → 쿨다운 없이 1초마다 1마리

            for (int i = 0; i < positions.Length; i++)
            {
                spawner.Advance(1f);
            }

            Assert.AreEqual(positions.Length, factory.CreateCallCount); // 준비 확인
            return spawner;
        }

        // ───────── 스폰 ─────────

        [Test]
        public void Advance_BeforeWaveStarted_DoesNotSpawn()
        {
            var factory = new FakeEnemyViewFactory(3f);
            var spawner = CreateSpawner(factory, out _, out _);

            spawner.Advance(5f); // WaveStarted가 안 왔으니 시간이 아무리 지나도 스폰 안 됨

            Assert.AreEqual(0, factory.CreateCallCount);
        }

        [Test]
        public void Advance_SpawnsExactlyOncePerFixedInterval()
        {
            var factory = new FakeEnemyViewFactory(3f);
            var spawner = CreateSpawner(factory, out var waveStarted, out _);
            StartWave(waveStarted, enemyCount: 5);

            spawner.Advance(0.9f); // 아직 1초 안 지남
            Assert.AreEqual(0, factory.CreateCallCount);

            spawner.Advance(0.2f); // 누적 1.1초 → 스폰
            Assert.AreEqual(1, factory.CreateCallCount);
        }

        [Test]
        public void Advance_MovesSpawnedEnemyUsingSpeedFromProvider()
        {
            var factory = new FakeEnemyViewFactory(3f);
            var spawner = CreateSpawner(factory, out var waveStarted, out _); // 벽은 멀리(-100)
            StartWave(waveStarted, enemyCount: 5);

            spawner.Advance(1f); // 스폰
            var spawnedY = factory.LastCreatedEnemy.Position.y;

            spawner.Advance(0.5f); // 다음 스폰 전 → 이동만
            var movedY = factory.LastCreatedEnemy.Position.y;

            Assert.AreEqual(spawnedY - 3f * 0.5f, movedY, 0.0001f);
        }

        [Test]
        public void Advance_SpawnsUsingSpawnAreaXAndYBoundsSeparately()
        {
            var spawnArea = CreateTestSpawnArea();
            spawnArea.transform.position = new Vector3(2f, 5f, 0f); // X, Y를 다르게 둬서 뒤바뀜을 잡음
            var random = new FakeRandomProvider(1f);
            var factory = new FakeEnemyViewFactory(3f);
            var spawner = CreateSpawner(factory, out var waveStarted, out _, spawnArea: spawnArea, random: random);
            StartWave(waveStarted, enemyCount: 5);

            spawner.Advance(1f); // 스폰

            CollectionAssert.Contains(random.Calls, (spawnArea.Min.x, spawnArea.Max.x));
            CollectionAssert.Contains(random.Calls, (spawnArea.Min.y, spawnArea.Max.y));
        }

        [Test]
        public void Advance_FillsBurst_WaitsCooldown_ThenSpawnsNextBurst()
        {
            var factory = new FakeEnemyViewFactory(3f);
            var spawner = CreateSpawner(factory, out var waveStarted, out _); // SpawnCooldown = 3
            StartWave(waveStarted, enemyCount: 2);                           // 한 버스트에 2마리

            spawner.Advance(1f); // 1번째
            spawner.Advance(1f); // 2번째 → 대기 상태
            Assert.AreEqual(2, factory.CreateCallCount);

            spawner.Advance(2.9f); // 쿨다운 전
            Assert.AreEqual(2, factory.CreateCallCount);

            spawner.Advance(0.2f); // 쿨다운 끝 (이 호출에선 스폰 X)
            Assert.AreEqual(2, factory.CreateCallCount);

            spawner.Advance(1f); // 다음 버스트 1번째
            Assert.AreEqual(3, factory.CreateCallCount);
        }

        // ───────── GetNearest ─────────

        [Test]
        public void GetNearest_NoEnemies_ReturnsZeroAndEmptyResults()
        {
            var spawner = CreateSpawnerWithEnemies(out _);
            var results = new List<IEnemyTarget>();

            int filled = spawner.GetNearest(Vector2.zero, 3, results);

            Assert.AreEqual(0, filled);
            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void GetNearest_ClearsResultsFirst()
        {
            var spawner = CreateSpawnerWithEnemies(out _, new Vector2(0f, 1f));
            var stale = CreateStandaloneEnemy();
            var results = new List<IEnemyTarget> { stale, stale }; // 이전 결과가 남아 있는 상태

            int filled = spawner.GetNearest(Vector2.zero, 3, results);

            Assert.AreEqual(1, filled);
            Assert.AreEqual(1, results.Count);
            CollectionAssert.DoesNotContain(results, stale);
        }

        [Test]
        public void GetNearest_SortsByDistanceFromOrigin()
        {
            var spawner = CreateSpawnerWithEnemies(out _,
                new Vector2(0f, 10f),
                new Vector2(0f, 2f),
                new Vector2(0f, 5f));
            var results = new List<IEnemyTarget>();

            int filled = spawner.GetNearest(Vector2.zero, 3, results);

            Assert.AreEqual(3, filled);
            Assert.AreEqual(2f, results[0].Position.y, 0.0001f);
            Assert.AreEqual(5f, results[1].Position.y, 0.0001f);
            Assert.AreEqual(10f, results[2].Position.y, 0.0001f);
        }

        [Test]
        public void GetNearest_LimitsToCount_KeepsClosest()
        {
            var spawner = CreateSpawnerWithEnemies(out _,
                new Vector2(0f, 10f),
                new Vector2(0f, 2f),
                new Vector2(0f, 5f));
            var results = new List<IEnemyTarget>();

            int filled = spawner.GetNearest(Vector2.zero, 2, results);

            Assert.AreEqual(2, filled);
            Assert.AreEqual(2, results.Count);
            Assert.AreEqual(2f, results[0].Position.y, 0.0001f);
            Assert.AreEqual(5f, results[1].Position.y, 0.0001f);
        }

        [Test]
        public void GetNearest_CountGreaterThanEnemies_ReturnsAllEnemies()
        {
            var spawner = CreateSpawnerWithEnemies(out _,
                new Vector2(0f, 1f),
                new Vector2(0f, 2f));
            var results = new List<IEnemyTarget>();

            int filled = spawner.GetNearest(Vector2.zero, 10, results);

            Assert.AreEqual(2, filled);
            Assert.AreEqual(2, results.Count);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void GetNearest_NonPositiveCount_ReturnsZeroAndEmptyResults(int count)
        {
            var spawner = CreateSpawnerWithEnemies(out _, new Vector2(0f, 1f));
            var results = new List<IEnemyTarget> { CreateStandaloneEnemy() };

            int filled = spawner.GetNearest(Vector2.zero, count, results);

            Assert.AreEqual(0, filled);
            Assert.AreEqual(0, results.Count); // count가 0 이하여도 비우는 건 해야 함
        }

        [Test]
        public void GetNearest_UsesBothXAndYDistance()
        {
            var spawner = CreateSpawnerWithEnemies(out _,
                new Vector2(0f, 0f),  // from(5,0)까지 거리 5
                new Vector2(5f, 3f)); // from(5,0)까지 거리 3
            var results = new List<IEnemyTarget>();

            spawner.GetNearest(new Vector2(5f, 0f), 1, results);

            Assert.AreEqual(new Vector2(5f, 3f), results[0].Position);
        }

        [Test]
        public void GetNearest_CalledTwice_ReturnsSameResult()
        {
            var spawner = CreateSpawnerWithEnemies(out _,
                new Vector2(0f, 3f),
                new Vector2(0f, 1f));
            var first = new List<IEnemyTarget>();
            var second = new List<IEnemyTarget>();

            spawner.GetNearest(Vector2.zero, 2, first);
            spawner.GetNearest(Vector2.zero, 2, second);

            CollectionAssert.AreEqual(first, second); // 같은 적, 같은 순서
        }

        // ───────── 사망 ─────────

        [Test]
        public void GetNearest_ExcludesDeadEnemy_EvenBeforeRemoval()
        {
            var spawner = CreateSpawnerWithEnemies(out var factory,
                new Vector2(0f, 1f),
                new Vector2(0f, 2f));
            var results = new List<IEnemyTarget>();

            factory.Created[0].TakeDamage(999); // 사망, 아직 Advance 전

            int filled = spawner.GetNearest(Vector2.zero, 5, results);

            Assert.AreEqual(1, filled);
            Assert.AreSame(factory.Created[1], results[0]);
        }

        [Test]
        public void Advance_RemovesDeadEnemy_KeepsAliveEnemy()
        {
            var spawner = CreateSpawnerWithEnemies(out var factory,
                new Vector2(0f, 1f),
                new Vector2(0f, 2f));
            var results = new List<IEnemyTarget>();

            factory.Created[0].TakeDamage(999);
            spawner.Advance(0.1f); // TickCombat에서 제거

            Assert.AreEqual(1, spawner.GetNearest(Vector2.zero, 5, results));
            Assert.AreSame(factory.Created[1], results[0]);
        }

        [Test]
        public void EnemyDied_PublishedOnceWithId()
        {
            var spawner = CreateSpawnerWithEnemies(out var factory, new Vector2(0f, 1f));
            var enemy = factory.Created[0];

            enemy.TakeDamage(999);
            spawner.Advance(0.1f);
            enemy.TakeDamage(999); // 이미 죽은 적을 또 때려도
            spawner.Advance(0.1f);

            Assert.AreEqual(1, factory.Died.Published.Count);
            Assert.AreEqual(enemy.Id, factory.Died.Published[0].EnemyId);
        }

        [Test]
        public void Advance_AllEnemiesDead_GetNearestReturnsZero()
        {
            var spawner = CreateSpawnerWithEnemies(out var factory,
                new Vector2(0f, 1f),
                new Vector2(0f, 2f),
                new Vector2(0f, 3f));
            var results = new List<IEnemyTarget>();

            foreach (var enemy in factory.Created)
            {
                enemy.TakeDamage(999);
            }
            spawner.Advance(0.1f);

            Assert.AreEqual(0, spawner.GetNearest(Vector2.zero, 5, results));
            Assert.AreEqual(3, factory.Died.Published.Count);
        }

        // ───────── 적 공격 (TickCombat) ─────────
        // 스폰 위치 (1, 1). enemyCount 1 → 스폰 후 3초 쿨다운 동안 추가 스폰 없음

        [Test]
        public void Advance_EnemyOutOfRange_KeepsMoving_NoDamage()
        {
            var wall = CreateTestWall(FarWallY);
            var factory = new FakeEnemyViewFactory(3f);
            var spawner = CreateSpawner(factory, out var waveStarted, out _, wall: wall);
            StartWave(waveStarted, enemyCount: 1);

            spawner.Advance(1f); // 스폰 (y = 1)
            spawner.Advance(1f); // 이동 → -2
            spawner.Advance(1f); // 이동 → -5

            Assert.AreEqual(1f - 3f * 2f, factory.LastCreatedEnemy.Position.y, 0.0001f);
            Assert.IsFalse(factory.LastCreatedEnemy.IsInAttackRange(wall));
            Assert.AreEqual(WallMaxHp, wall.CurrentHp);
        }

        [Test]
        public void Advance_MeleeEnemy_MovesToWall_ThenDamagesEveryInterval()
        {
            var wall = CreateTestWall(-10f);
            var factory = new FakeEnemyViewFactory(11f); // 1초에 11 → y 1 → -10 (벽 라인)
            var spawner = CreateSpawner(factory, out var waveStarted, out _, wall: wall);
            StartWave(waveStarted, enemyCount: 1);

            spawner.Advance(1f); // 스폰
            spawner.Advance(1f); // 이동 → -10, 이 프레임은 이동만
            Assert.AreEqual(-10f, factory.LastCreatedEnemy.Position.y, 0.0001f);
            Assert.AreEqual(WallMaxHp, wall.CurrentHp);

            spawner.Advance(1f); // 공격 1회
            Assert.AreEqual(WallMaxHp - 10, wall.CurrentHp);

            spawner.Advance(0.5f); // 간격 전
            Assert.AreEqual(WallMaxHp - 10, wall.CurrentHp);

            spawner.Advance(0.5f); // 누적 1초 → 공격 1회
            Assert.AreEqual(WallMaxHp - 20, wall.CurrentHp);

            Assert.AreEqual(-10f, factory.LastCreatedEnemy.Position.y, 0.0001f); // 공격 중엔 이동 안 함
        }

        [Test]
        public void Advance_RangedEnemy_StopsAtRange_ProjectileDamagesWall()
        {
            var wall = CreateTestWall(-10f);
            var projectiles = new EnemyProjectileSystem();
            var ranged = new EnemyAttackStats(AttackType.Ranged, 10, 1f, 4f, 8f); // 사거리 4 → y -6에서 공격
            var factory = new FakeEnemyViewFactory(7f, attack: ranged);       // 1초에 7 → y 1 → -6
            var spawner = CreateSpawner(factory, out var waveStarted, out _, wall: wall, projectiles: projectiles);
            StartWave(waveStarted, enemyCount: 1);

            int fired = 0;
            spawner.Advance(1f); // 스폰
            factory.LastCreatedEnemy.ProjectileFired += _ => fired++;

            spawner.Advance(1f); // 이동 → -6
            Assert.IsTrue(factory.LastCreatedEnemy.IsInAttackRange(wall));
            Assert.AreEqual(0, fired);

            spawner.Advance(1f); // 발사 → 같은 프레임에 투사체도 1초 이동(8) → -14 ≤ -10 명중

            Assert.AreEqual(1, fired);
            Assert.AreEqual(WallMaxHp - 10, wall.CurrentHp);
            Assert.AreEqual(0, projectiles.ActiveCount);
            Assert.AreEqual(-6f, factory.LastCreatedEnemy.Position.y, 0.0001f); // 사거리 지점에서 멈춤
        }

        [Test]
        public void Advance_DeadEnemy_StopsAttacking()
        {
            var wall = CreateTestWall(-10f);
            var factory = new FakeEnemyViewFactory(11f);
            var spawner = CreateSpawner(factory, out var waveStarted, out _, wall: wall);
            StartWave(waveStarted, enemyCount: 1);

            spawner.Advance(1f); // 스폰
            spawner.Advance(1f); // 이동 → 벽 라인
            factory.LastCreatedEnemy.TakeDamage(999);
            spawner.Advance(1f); // 죽은 적은 목록에서 제거, 공격 없음

            Assert.AreEqual(WallMaxHp, wall.CurrentHp);
        }

        [Test]
        public void Advance_AfterFinalBurst_ExistingEnemiesKeepAttacking()
        {
            var wall = CreateTestWall(-10f);
            var factory = new FakeEnemyViewFactory(11f);
            var spawner = CreateSpawner(factory, out var waveStarted, out _, wall: wall);
            StartWave(waveStarted, enemyCount: 1, isFinal: true);

            spawner.Advance(1f); // 스폰 → 마지막 웨이브 버스트 완료로 스폰 멈춤

            spawner.Advance(1f); // 이동 → 벽 라인
            spawner.Advance(1f); // 공격

            Assert.AreEqual(1, factory.CreateCallCount);
            Assert.AreEqual(WallMaxHp - 10, wall.CurrentHp);
        }

        [Test]
        public void Advance_WallDestroyed_NoMoreDamage()
        {
            var wall = CreateTestWall(-10f, maxHp: 15);
            var factory = new FakeEnemyViewFactory(11f);
            var spawner = CreateSpawner(factory, out var waveStarted, out _, wall: wall);
            StartWave(waveStarted, enemyCount: 1);

            spawner.Advance(1f); // 스폰
            spawner.Advance(1f); // 이동
            spawner.Advance(1f); // 15 → 5
            spawner.Advance(1f); // 5 → 0, 파괴
            spawner.Advance(1f); // 파괴 후 공격 안 함

            Assert.AreEqual(0, wall.CurrentHp);
            Assert.IsTrue(wall.IsDestroyed);
        }

        [Test]
        public void FinalWave_SpawnsOneBurstOnly()
        {
            var factory = new FakeEnemyViewFactory(3f);
            var spawner = CreateSpawner(factory, out var waveStarted, out _);
            waveStarted.Publish(new WaveStarted(3, enemyCount: 2, isFinalWave: true));

            spawner.Advance(1f);
            spawner.Advance(1f);
            Assert.AreEqual(2, factory.CreateCallCount);

            spawner.Advance(10f); // 쿨다운(3초)이 지나도 재스폰 없음
            Assert.AreEqual(2, factory.CreateCallCount);
        }

        [Test]
        public void FinalWave_EnemiesAlive_DoesNotPublishCleared()
        {
            var factory = new FakeEnemyViewFactory(3f);
            var spawner = CreateSpawner(factory, out var waveStarted, out var allCleared);
            waveStarted.Publish(new WaveStarted(3, enemyCount: 1, isFinalWave: true));

            spawner.Advance(1f); // 1마리 스폰 → 버스트 완료
            spawner.Advance(1f);

            Assert.AreEqual(0, allCleared.Published.Count);
        }

        [Test]
        public void FinalWave_AllDead_PublishesClearedOnce()
        {
            var factory = new FakeEnemyViewFactory(3f);
            var spawner = CreateSpawner(factory, out var waveStarted, out var allCleared);
            waveStarted.Publish(new WaveStarted(3, enemyCount: 1, isFinalWave: true));

            spawner.Advance(1f);
            factory.Created[0].TakeDamage(999); // 스폰된 적 처치

            spawner.Advance(0.1f);
            spawner.Advance(0.1f);

            Assert.AreEqual(1, allCleared.Published.Count);
        }

        [Test]
        public void NonFinalWave_AllDead_DoesNotPublishCleared()
        {
            var factory = new FakeEnemyViewFactory(3f);
            var spawner = CreateSpawner(factory, out var waveStarted, out var allCleared);
            waveStarted.Publish(new WaveStarted(1, enemyCount: 1, isFinalWave: false));

            spawner.Advance(1f);
            factory.Created[0].TakeDamage(999); // 스폰된 적 처치

            spawner.Advance(0.1f);
            Assert.AreEqual(0, allCleared.Published.Count);
        }

        [Test]
        public void FinalWave_AllDead_WhilePaused_PublishesAfterResume()
        {
            var factory = new FakeEnemyViewFactory(3f);
            var spawner = CreateSpawner(factory, out var waveStarted, out var allCleared);
            StartWave(waveStarted, enemyCount: 1, isFinal: true);

            spawner.Advance(1f);
            factory.Created[0].TakeDamage(999);

            spawner.Advance(0f); // 일시정지 중 (timeScale 0)
            spawner.Advance(0f);
            Assert.AreEqual(0, allCleared.Published.Count);

            spawner.Advance(0.1f); // 재개
            Assert.AreEqual(1, allCleared.Published.Count);
        }
    }
}
