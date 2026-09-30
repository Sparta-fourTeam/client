using System;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Messages;
using MessagePipe;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public class EnemySpawnerTests
    {
        // ───────── Fakes ─────────

        private class FakePublisher<T> : IPublisher<T>
        {
            public readonly List<T> Published = new();
            public void Publish(T message) => Published.Add(message);
        }

        private class FakeEnemyViewFactory : IEnemyFactory
        {
            private readonly float _speed;
            private readonly int _maxHp;
            private int _nextId;

            public int CreateCallCount;
            public EnemyModel LastCreatedEnemy;
            public readonly List<EnemyModel> Created = new();
            public readonly FakePublisher<EnemyHpChanged> HpChanged = new();
            public readonly FakePublisher<EnemyDied> Died = new();

            // 채워두면 스폰 위치 대신 이 위치를 순서대로 사용 (GetNearest 테스트용)
            public readonly Queue<Vector2> PositionOverrides = new();

            public FakeEnemyViewFactory(float speed, int maxHp = 10)
            {
                _speed = speed;
                _maxHp = maxHp;
            }

            public EnemyModel Create(Vector2 spawnPosition, EnemyType type)
            {
                var position = PositionOverrides.Count > 0 ? PositionOverrides.Dequeue() : spawnPosition;

                CreateCallCount++;
                LastCreatedEnemy = new EnemyModel(++_nextId, position, _speed, type, _maxHp, HpChanged, Died);
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

        // 테스트에서 WaveStarted / WaveGaugeFilled를 직접 발행할 수 있게 해주는 가짜 구독자
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

        // ───────── Helpers ─────────

        private static EnemySpawnConfig CreateTestWaveData()
        {
            return new EnemySpawnConfig(
                spawnIntervalMin: 0.1f,
                spawnIntervalMax: 0.5f,
                spawnCooldown: 3f,
                enemyType: EnemyType.Normal);
        }

        private static SpawnArea CreateTestSpawnArea()
        {
            var go = new GameObject("SpawnArea");
            return go.AddComponent<SpawnArea>();
        }

        // 스포너와 무관하게 쓰는 더미 적
        private static EnemyModel CreateStandaloneEnemy()
        {
            return new EnemyModel(0, Vector2.zero, 0f, EnemyType.Normal, 10,
                new FakePublisher<EnemyHpChanged>(), new FakePublisher<EnemyDied>());
        }

        // 지정한 위치에 적을 스폰해 둔 스포너. 속도 0이라 적이 움직이지 않음
        private static EnemySpawner CreateSpawnerWithEnemies(out FakeEnemyViewFactory factory, params Vector2[] positions)
        {
            factory = new FakeEnemyViewFactory(0f);
            foreach (var position in positions)
            {
                factory.PositionOverrides.Enqueue(position);
            }

            var waveStarted = new FakeSubscriber<WaveStarted>();
            var spawner = new EnemySpawner(
                factory,
                CreateTestWaveData(),
                CreateTestSpawnArea(),
                new FakeRandomProvider(1f), // 스폰 간격 1초
                waveStarted,
                new FakeSubscriber<WaveGaugeFilled>());
            spawner.Initialize();

            // 버스트를 넉넉히 줘서 쿨다운 없이 1초마다 1마리씩 스폰
            waveStarted.Publish(new WaveStarted(waveIndex: 1, enemyCount: 10, isFinalWave: false));
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
            var waveData = CreateTestWaveData();
            var spawnArea = CreateTestSpawnArea();
            var factory = new FakeEnemyViewFactory(3.0f);
            var random = new FakeRandomProvider(1f);
            var waveStarted = new FakeSubscriber<WaveStarted>();
            var waveGaugeFilled = new FakeSubscriber<WaveGaugeFilled>();

            var spawner = new EnemySpawner(factory, waveData, spawnArea, random, waveStarted, waveGaugeFilled);
            spawner.Initialize();

            spawner.Advance(5f); // WaveStarted가 안 왔으니 시간이 아무리 지나도 스폰 안 됨

            Assert.AreEqual(0, factory.CreateCallCount);
        }

        [Test]
        public void Advance_SpawnsExactlyOncePerFixedInterval()
        {
            var waveData = CreateTestWaveData();
            var spawnArea = CreateTestSpawnArea();
            var factory = new FakeEnemyViewFactory(3.0f);
            var random = new FakeRandomProvider(1f); // 랜덤이지만 항상 1초로 고정
            var waveStarted = new FakeSubscriber<WaveStarted>();
            var waveGaugeFilled = new FakeSubscriber<WaveGaugeFilled>();

            var spawner = new EnemySpawner(factory, waveData, spawnArea, random, waveStarted, waveGaugeFilled);
            spawner.Initialize();
            waveStarted.Publish(new WaveStarted(waveIndex: 0, enemyCount: 5, isFinalWave: false));

            spawner.Advance(0.9f); // 아직 1초 안 지남
            Assert.AreEqual(0, factory.CreateCallCount);

            spawner.Advance(0.2f); // 누적 1.1초 -> 스폰돼야 함
            Assert.AreEqual(1, factory.CreateCallCount);
        }

        [Test]
        public void Advance_MovesSpawnedEnemyUsingSpeedFromProvider()
        {
            var waveData = CreateTestWaveData();
            var spawnArea = CreateTestSpawnArea();
            var factory = new FakeEnemyViewFactory(3.0f);
            var random = new FakeRandomProvider(1f);
            var waveStarted = new FakeSubscriber<WaveStarted>();
            var waveGaugeFilled = new FakeSubscriber<WaveGaugeFilled>();

            var spawner = new EnemySpawner(factory, waveData, spawnArea, random, waveStarted, waveGaugeFilled);
            spawner.Initialize();
            waveStarted.Publish(new WaveStarted(waveIndex: 0, enemyCount: 5, isFinalWave: false));

            spawner.Advance(1f); // 스폰 발생
            var spawnedY = factory.LastCreatedEnemy.Position.y;

            spawner.Advance(0.5f); // 다음 스폰 간격(1초) 안 지남 -> 이동만 발생
            var movedY = factory.LastCreatedEnemy.Position.y;

            Assert.AreEqual(spawnedY - 3f * 0.5f, movedY, 0.0001f);
        }

        [Test]
        public void Advance_SpawnsUsingSpawnAreaXAndYBoundsSeparately()
        {
            var waveData = CreateTestWaveData();
            var spawnArea = CreateTestSpawnArea();
            spawnArea.transform.position = new Vector3(2f, 5f, 0f); // X, Y를 다른 값으로 둬서 뒤바뀜을 잡을 수 있게 함

            var factory = new FakeEnemyViewFactory(3.0f);
            var random = new FakeRandomProvider(1f);
            var waveStarted = new FakeSubscriber<WaveStarted>();
            var waveGaugeFilled = new FakeSubscriber<WaveGaugeFilled>();

            var spawner = new EnemySpawner(factory, waveData, spawnArea, random, waveStarted, waveGaugeFilled);
            spawner.Initialize();
            waveStarted.Publish(new WaveStarted(waveIndex: 0, enemyCount: 5, isFinalWave: false));

            spawner.Advance(1f); // 스폰 발생

            var expectedXCall = (spawnArea.Min.x, spawnArea.Max.x);
            var expectedYCall = (spawnArea.Min.y, spawnArea.Max.y);

            CollectionAssert.Contains(random.Calls, expectedXCall);
            CollectionAssert.Contains(random.Calls, expectedYCall);
        }

        [Test]
        public void Advance_AfterWaveGaugeFilled_StopsSpawning()
        {
            var waveData = CreateTestWaveData();
            var spawnArea = CreateTestSpawnArea();
            var factory = new FakeEnemyViewFactory(3.0f);
            var random = new FakeRandomProvider(1f);
            var waveStarted = new FakeSubscriber<WaveStarted>();
            var waveGaugeFilled = new FakeSubscriber<WaveGaugeFilled>();

            var spawner = new EnemySpawner(factory, waveData, spawnArea, random, waveStarted, waveGaugeFilled);
            spawner.Initialize();
            waveStarted.Publish(new WaveStarted(waveIndex: 0, enemyCount: 5, isFinalWave: false));

            spawner.Advance(1f); // 스폰 1회 발생
            Assert.AreEqual(1, factory.CreateCallCount);

            waveGaugeFilled.Publish(new WaveGaugeFilled(isFinalWave: false)); // 웨이브 종료

            spawner.Advance(10f); // 다음 WaveStarted 전까지는 시간이 지나도 스폰 안 됨
            Assert.AreEqual(1, factory.CreateCallCount);
        }

        [Test]
        public void Advance_FillsBurst_WaitsCooldown_ThenSpawnsNextBurst()
        {
            var waveData = CreateTestWaveData(); // SpawnCooldown = 3f
            var spawnArea = CreateTestSpawnArea();
            var factory = new FakeEnemyViewFactory(3.0f);
            var random = new FakeRandomProvider(1f); // Range가 항상 1초/1유닛 고정
            var waveStarted = new FakeSubscriber<WaveStarted>();
            var waveGaugeFilled = new FakeSubscriber<WaveGaugeFilled>();

            var spawner = new EnemySpawner(factory, waveData, spawnArea, random, waveStarted, waveGaugeFilled);
            spawner.Initialize();
            waveStarted.Publish(new WaveStarted(waveIndex: 0, enemyCount: 2, isFinalWave: false)); // 이번 웨이브는 한 버스트에 2마리

            spawner.Advance(1f); // 버스트 1번째 스폰
            spawner.Advance(1f); // 버스트 2번째 스폰 -> 버스트 크기(2) 채움 -> 대기 상태 전환
            Assert.AreEqual(2, factory.CreateCallCount);

            spawner.Advance(2.9f); // 쿨다운(3초) 아직 안 지남 -> 계속 대기
            Assert.AreEqual(2, factory.CreateCallCount);

            spawner.Advance(0.2f); // 누적 3.1초 -> 쿨다운 끝, 대기 상태 해제(이 호출에서 바로 스폰되진 않음)
            Assert.AreEqual(2, factory.CreateCallCount);

            spawner.Advance(1f); // 다음 스폰 간격 지남 -> 다음 버스트의 1번째 스폰 -> 계속 반복돼야 함
            Assert.AreEqual(3, factory.CreateCallCount);
        }

        // ───────── GetNearest ─────────

        [Test]
        public void GetNearest_NoEnemies_ReturnsZeroAndEmptyResults()
        {
            var provider = CreateSpawnerWithEnemies(out _);
            var results = new List<IEnemyTarget>();

            int filled = provider.GetNearest(Vector2.zero, 3, results);

            Assert.AreEqual(0, filled);
            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void GetNearest_ClearsResultsFirst()
        {
            var provider = CreateSpawnerWithEnemies(out _, new Vector2(0f, 1f));
            var stale = CreateStandaloneEnemy();
            var results = new List<IEnemyTarget> { stale, stale }; // 이전 결과가 남아 있는 상태

            int filled = provider.GetNearest(Vector2.zero, 3, results);

            Assert.AreEqual(1, filled);
            Assert.AreEqual(1, results.Count);
            CollectionAssert.DoesNotContain(results, stale);
        }

        [Test]
        public void GetNearest_SortsByDistanceFromOrigin()
        {
            var provider = CreateSpawnerWithEnemies(out _,
                new Vector2(0f, 10f),
                new Vector2(0f, 2f),
                new Vector2(0f, 5f));
            var results = new List<IEnemyTarget>();

            int filled = provider.GetNearest(Vector2.zero, 3, results);

            Assert.AreEqual(3, filled);
            Assert.AreEqual(2f, results[0].Position.y, 0.0001f);
            Assert.AreEqual(5f, results[1].Position.y, 0.0001f);
            Assert.AreEqual(10f, results[2].Position.y, 0.0001f);
        }

        [Test]
        public void GetNearest_LimitsToCount_KeepsClosest()
        {
            var provider = CreateSpawnerWithEnemies(out _,
                new Vector2(0f, 10f),
                new Vector2(0f, 2f),
                new Vector2(0f, 5f));
            var results = new List<IEnemyTarget>();

            int filled = provider.GetNearest(Vector2.zero, 2, results);

            Assert.AreEqual(2, filled);
            Assert.AreEqual(2, results.Count);
            Assert.AreEqual(2f, results[0].Position.y, 0.0001f);
            Assert.AreEqual(5f, results[1].Position.y, 0.0001f);
        }

        [Test]
        public void GetNearest_CountGreaterThanEnemies_ReturnsAllEnemies()
        {
            var provider = CreateSpawnerWithEnemies(out _,
                new Vector2(0f, 1f),
                new Vector2(0f, 2f));
            var results = new List<IEnemyTarget>();

            int filled = provider.GetNearest(Vector2.zero, 10, results);

            Assert.AreEqual(2, filled);
            Assert.AreEqual(2, results.Count);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void GetNearest_NonPositiveCount_ReturnsZeroAndEmptyResults(int count)
        {
            var provider = CreateSpawnerWithEnemies(out _, new Vector2(0f, 1f));
            var results = new List<IEnemyTarget> { CreateStandaloneEnemy() };

            int filled = provider.GetNearest(Vector2.zero, count, results);

            Assert.AreEqual(0, filled);
            Assert.AreEqual(0, results.Count); // count가 0 이하여도 비우는 건 해야 함
        }

        [Test]
        public void GetNearest_UsesBothXAndYDistance()
        {
            var provider = CreateSpawnerWithEnemies(out _,
                new Vector2(0f, 0f),  // from(5,0)까지 거리 5
                new Vector2(5f, 3f)); // from(5,0)까지 거리 3
            var results = new List<IEnemyTarget>();

            provider.GetNearest(new Vector2(5f, 0f), 1, results);

            Assert.AreEqual(new Vector2(5f, 3f), results[0].Position);
        }

        [Test]
        public void GetNearest_CalledTwice_ReturnsSameResult()
        {
            var provider = CreateSpawnerWithEnemies(out _,
                new Vector2(0f, 3f),
                new Vector2(0f, 1f));
            var first = new List<IEnemyTarget>();
            var second = new List<IEnemyTarget>();

            provider.GetNearest(Vector2.zero, 2, first);
            provider.GetNearest(Vector2.zero, 2, second);

            CollectionAssert.AreEqual(first, second); // 같은 적, 같은 순서
        }

        // ───────── 사망 연동 ─────────

        [Test]
        public void GetNearest_ExcludesDeadEnemy_EvenBeforeRemoval()
        {
            var spawner = CreateSpawnerWithEnemies(out var factory,
                new Vector2(0f, 1f),
                new Vector2(0f, 2f));
            var results = new List<IEnemyTarget>();

            factory.Created[0].TakeDamage(999); // (0,1) 적 사망, 아직 Advance 전

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
            spawner.Advance(0.1f); // 죽은 적 제거

            int filled = spawner.GetNearest(Vector2.zero, 5, results);

            Assert.AreEqual(1, filled);
            Assert.AreSame(factory.Created[1], results[0]);
        }

        [Test]
        public void Advance_AfterEnemyDies_DoesNotPublishEnemyDiedAgain()
        {
            var spawner = CreateSpawnerWithEnemies(out var factory, new Vector2(0f, 1f));

            factory.Created[0].TakeDamage(999); // EnemyModel이 발행 (1회)
            spawner.Advance(0.1f);              // 스포너는 제거만 하고 발행 안 함
            spawner.Advance(0.1f);

            Assert.AreEqual(1, factory.Died.Published.Count);
            Assert.AreEqual(factory.Created[0].Id, factory.Died.Published[0].EnemyId);
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
    }
}