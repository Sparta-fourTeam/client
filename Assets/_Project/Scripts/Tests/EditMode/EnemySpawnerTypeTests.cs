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
    // 웨이브 구성(몬스터×마릿수)대로 스폰하는지, 엘리트·보스가 웨이브당 한 번만 나오는지 테스트
    public class EnemySpawnerTypeTests
    {
        // ───────── Fakes ─────────

        private class FakeWallPublisher<T> : IPublisher<T>, IBufferedPublisher<T>
        {
            public void Publish(T message) { }
        }

        private class FakePublisher<T> : IPublisher<T>
        {
            public void Publish(T message) { }
        }

        // 어떤 타입으로 Create가 불렸는지 순서대로 기록
        private class TypeRecordingFactory : IEnemyFactory
        {
            private static readonly EnemyAttackStats Attack = new EnemyAttackStats(AttackType.Melee, 1, 1f, 0f);
            private int _nextId;

            public readonly List<EnemyType> Types = new();

            // 등급별 몬스터 Id: 일반 1, 엘리트 2, 보스 3
            public const int NormalId = 1, EliteId = 2, BossId = 3;

            public Enemy Create(int monsterId, Vector2 spawnPosition, bool isSummoned = false)
            {
                var type = monsterId == BossId ? EnemyType.Boss : monsterId == EliteId ? EnemyType.Elite : EnemyType.Normal;
                Types.Add(type);
                // 속도 0: 움직이지 않아 벽 사거리에 들어가지 않음
                return TestEnemy.Create(new EnemyModel(++_nextId, 0f, type, 10, Attack,
                    new FakePublisher<EnemyHpChanged>(), new FakePublisher<EnemyDied>()), spawnPosition);
            }

            public int Count(EnemyType type) => Types.FindAll(t => t == type).Count;
        }

        // 스폰 간격·위치·섞기 모두 같은 값(1)을 받는다
        private class FixedRandomProvider : IRandomProvider
        {
            public float Range(float min, float max) => 1f;
        }

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

        private const float SpawnCooldown = 3f;

        private readonly List<GameObject> _createdObjects = new();

        private TypeRecordingFactory _factory;
        private FixedRandomProvider _random;
        private FakeSubscriber<WaveStarted> _waveStarted;
        private FakePublisher<AllEnemiesCleared> _allCleared;

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

        private EnemySpawner CreateSpawner()
        {
            var wallGo = new GameObject("Wall");
            _createdObjects.Add(wallGo);
            wallGo.transform.position = new Vector3(0f, -100f, 0f);
            var wall = wallGo.AddComponent<Wall>();
            wall.Construct(new FakeWallPublisher<WallHpChanged>(), new FakeWallPublisher<WallDestroyed>(), null);
            wall.Initialize(100);

            var areaGo = new GameObject("SpawnArea");
            _createdObjects.Add(areaGo);
            var spawnArea = areaGo.AddComponent<SpawnArea>(); // 크기 0 → 위치 Range는 (0, 0)으로 호출됨

            var config = new EnemySpawnConfig(
                spawnIntervalMin: 0.1f,
                spawnIntervalMax: 0.5f,
                spawnCooldown: SpawnCooldown);

            _factory = new TypeRecordingFactory();
            _random = new FixedRandomProvider();
            _waveStarted = new FakeSubscriber<WaveStarted>();
            _allCleared = new FakePublisher<AllEnemiesCleared>();

            var spawner = new EnemySpawner(
                _factory, config, spawnArea, _random,
                _waveStarted, _allCleared,
                wall, new EnemyProjectileSystem());
            spawner.Initialize();
            return spawner;
        }

        private static WaveSpawn N(int count) => new() { MonsterId = TypeRecordingFactory.NormalId, Count = count, Type = EnemyType.Normal };
        private static WaveSpawn E(int count = 1) => new() { MonsterId = TypeRecordingFactory.EliteId, Count = count, Type = EnemyType.Elite };
        private static WaveSpawn B(int count = 1) => new() { MonsterId = TypeRecordingFactory.BossId, Count = count, Type = EnemyType.Boss };

        private void StartWave(bool isFinal, params WaveSpawn[] spawns)
        {
            _waveStarted.Publish(new WaveStarted(1, isFinal, spawns));
        }

        // 스폰 간격 1초, 쿨다운 3초 → 1초씩 진행하며 총 count마리가 될 때까지 (버스트 넘김 포함)
        private void AdvanceUntilSpawned(EnemySpawner spawner, int count)
        {
            int guard = 0;
            while (_factory.Types.Count < count && guard++ < 1000)
            {
                spawner.Advance(1f);
            }
            Assert.AreEqual(count, _factory.Types.Count, "스폰 수가 기대와 다름");
        }

        // 더 나올 몬스터가 없는지: 충분히 진행해도 스폰 수가 그대로다
        private void AssertNothingMoreSpawns(EnemySpawner spawner)
        {
            int before = _factory.Types.Count;
            for (int i = 0; i < 20; i++) { spawner.Advance(1f); }
            Assert.AreEqual(before, _factory.Types.Count, "더 나오면 안 된다");
        }

        [Test]
        public void FirstBurst_SpawnsExactlyTheWaveComposition()
        {
            var spawner = CreateSpawner();
            StartWave(false, N(3), E(), B());

            AdvanceUntilSpawned(spawner, 5);

            Assert.AreEqual(3, _factory.Count(EnemyType.Normal));
            Assert.AreEqual(1, _factory.Count(EnemyType.Elite));
            Assert.AreEqual(1, _factory.Count(EnemyType.Boss));
        }

        [Test(Description = "처치가 늦어 다음 묶음이 나와도 엘리트·보스는 다시 나오지 않고 일반 몬스터만 반복된다")]
        public void LaterBursts_RepeatOnlyNormals()
        {
            var spawner = CreateSpawner();
            StartWave(false, N(2), E(), B());

            AdvanceUntilSpawned(spawner, 4 + 2 + 2); // 첫 묶음 4마리 + 일반 2마리씩 두 번 더

            Assert.AreEqual(1, _factory.Count(EnemyType.Boss));
            Assert.AreEqual(1, _factory.Count(EnemyType.Elite));
            Assert.AreEqual(6, _factory.Count(EnemyType.Normal));
        }

        [Test(Description = "첫 묶음은 구성의 마릿수 합계이고 반복 묶음은 일반 몬스터 수다")]
        public void BurstSize_IsCompositionTotalThenNormalCount()
        {
            var spawner = CreateSpawner();
            StartWave(false, N(2), E(), B());

            for (int i = 0; i < 4; i++) { spawner.Advance(1f); }
            Assert.AreEqual(4, _factory.Types.Count);

            spawner.Advance(1f); spawner.Advance(1f);
            Assert.AreEqual(4, _factory.Types.Count, "쿨다운 중에는 나오지 않는다");

            spawner.Advance(1f); // 누적 3초 → 쿨다운 끝
            spawner.Advance(1f);
            spawner.Advance(1f);
            Assert.AreEqual(6, _factory.Types.Count);
            Assert.AreEqual(EnemyType.Normal, _factory.Types[4]);
            Assert.AreEqual(EnemyType.Normal, _factory.Types[5]);
        }

        [Test]
        public void FinalWave_StopsAfterOneBurst()
        {
            var spawner = CreateSpawner();
            StartWave(true, N(1), B());

            AdvanceUntilSpawned(spawner, 2);

            AssertNothingMoreSpawns(spawner);
            Assert.AreEqual(1, _factory.Count(EnemyType.Boss));
            Assert.AreEqual(1, _factory.Count(EnemyType.Normal));
        }

        [Test(Description = "일반 몬스터가 없는 웨이브는 구성을 한 번 내고 나면 더 낼 것이 없다")]
        public void SpecialsOnlyWave_SpawnsOnceThenStops()
        {
            var spawner = CreateSpawner();
            StartWave(false, E(), B());

            AdvanceUntilSpawned(spawner, 2);

            AssertNothingMoreSpawns(spawner);
        }

        [Test(Description = "다음 웨이브는 자기 구성으로 다시 시작해 앞 웨이브의 보스를 이어받지 않는다")]
        public void EachWave_SpawnsItsOwnComposition()
        {
            var spawner = CreateSpawner();
            StartWave(true, N(1), B());
            AdvanceUntilSpawned(spawner, 2);

            StartWave(true, N(1), B());
            AdvanceUntilSpawned(spawner, 4);

            Assert.AreEqual(2, _factory.Count(EnemyType.Boss));
            Assert.AreEqual(2, _factory.Count(EnemyType.Normal));
        }

        [Test(Description = "구성이 없는 웨이브(처치 수만 알리는 메시지)는 아무것도 내지 않는다")]
        public void WaveWithoutComposition_SpawnsNothing()
        {
            var spawner = CreateSpawner();
            _waveStarted.Publish(new WaveStarted(1, 5, false));

            AssertNothingMoreSpawns(spawner);
            Assert.AreEqual(0, _factory.Types.Count);
        }
    }
}
