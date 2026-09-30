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
    // 웨이브별 Normal / Elite / Boss 타입 선택 테스트
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

            public EnemyModel Create(Vector2 spawnPosition, EnemyType type)
            {
                Types.Add(type);
                // 속도 0: 움직이지 않아 벽 사거리에 들어가지 않음
                return new EnemyModel(++_nextId, spawnPosition, 0f, type, 10, Attack,
                    new FakePublisher<EnemyHpChanged>(), new FakePublisher<EnemyDied>());
            }

            public int Count(EnemyType type) => Types.FindAll(t => t == type).Count;
        }

        // Range(0, 1)은 엘리트 판정 → EliteRoll, 그 외(스폰 간격·위치)는 1
        private class ScriptedRandomProvider : IRandomProvider
        {
            public float EliteRoll;
            public int EliteRollCallCount;

            public float Range(float min, float max)
            {
                if (min == 0f && max == 1f)
                {
                    EliteRollCallCount++;
                    return EliteRoll;
                }
                return 1f;
            }
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
        private ScriptedRandomProvider _random;
        private FakeSubscriber<WaveStarted> _waveStarted;
        private FakeSubscriber<WaveGaugeFilled> _waveGaugeFilled;

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

        private EnemySpawner CreateSpawner(float eliteSpawnChance)
        {
            var wallGo = new GameObject("Wall");
            _createdObjects.Add(wallGo);
            wallGo.transform.position = new Vector3(0f, -100f, 0f);
            var wall = wallGo.AddComponent<Wall>();
            wall.Construct(new FakeWallPublisher<WallHpChanged>(), new FakeWallPublisher<WallDestroyed>());
            wall.Initialize(100);

            var areaGo = new GameObject("SpawnArea");
            _createdObjects.Add(areaGo);
            var spawnArea = areaGo.AddComponent<SpawnArea>(); // 크기 0 → 위치 Range는 (0, 0)으로 호출됨

            var config = new EnemySpawnConfig(
                spawnIntervalMin: 0.1f,
                spawnIntervalMax: 0.5f,
                spawnCooldown: SpawnCooldown,
                eliteSpawnChance: eliteSpawnChance);

            _factory = new TypeRecordingFactory();
            _random = new ScriptedRandomProvider();
            _waveStarted = new FakeSubscriber<WaveStarted>();
            _waveGaugeFilled = new FakeSubscriber<WaveGaugeFilled>();

            var spawner = new EnemySpawner(
                _factory, config, spawnArea, _random,
                _waveStarted, _waveGaugeFilled,
                wall, new EnemyProjectileSystem());
            spawner.Initialize();
            return spawner;
        }

        private void StartWave(int enemyCount, int maxElite = 0, int maxBoss = 0)
        {
            _waveStarted.Publish(new WaveStarted(1, enemyCount, false, maxElite, maxBoss));
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

        // ───────── Normal ─────────

        [Test]
        public void NoEliteNoBoss_SpawnsOnlyNormal()
        {
            var spawner = CreateSpawner(eliteSpawnChance: 1f);
            _random.EliteRoll = 0f; // 판정은 무조건 성공하는 값이지만
            StartWave(enemyCount: 3);  // 최대 수가 0이라

            AdvanceUntilSpawned(spawner, 9); // 3버스트

            Assert.AreEqual(9, _factory.Count(EnemyType.Normal));
            Assert.AreEqual(0, _random.EliteRollCallCount); // 남은 엘리트가 없으면 판정 자체를 안 함
        }

        [Test]
        public void Normal_KeepsSpawningAcrossBursts_UntilGaugeFilled()
        {
            var spawner = CreateSpawner(eliteSpawnChance: 0f);
            StartWave(enemyCount: 2, maxElite: 1, maxBoss: 1);

            AdvanceUntilSpawned(spawner, 10);
            Assert.AreEqual(9, _factory.Count(EnemyType.Normal)); // 보스 1 + 노멀 9

            _waveGaugeFilled.Publish(new WaveGaugeFilled(false));
            spawner.Advance(10f);
            spawner.Advance(10f);

            Assert.AreEqual(10, _factory.Types.Count); // 게이지가 차면 멈춤
        }

        // ───────── Boss ─────────

        [Test]
        public void BossWave_FirstSpawnIsBoss_OnlyOnceAcrossBursts()
        {
            var spawner = CreateSpawner(eliteSpawnChance: 0f);
            StartWave(enemyCount: 3, maxBoss: 1);

            AdvanceUntilSpawned(spawner, 9);

            Assert.AreEqual(EnemyType.Boss, _factory.Types[0]);
            Assert.AreEqual(1, _factory.Count(EnemyType.Boss));
            Assert.AreEqual(8, _factory.Count(EnemyType.Normal));
        }

        // ───────── Elite ─────────

        [Test]
        public void EliteRollSuccess_SpawnsEliteUpToMaxOnly()
        {
            var spawner = CreateSpawner(eliteSpawnChance: 1f);
            _random.EliteRoll = 0f;
            StartWave(enemyCount: 5, maxElite: 2);

            AdvanceUntilSpawned(spawner, 10);

            Assert.AreEqual(EnemyType.Elite, _factory.Types[0]);
            Assert.AreEqual(EnemyType.Elite, _factory.Types[1]);
            Assert.AreEqual(2, _factory.Count(EnemyType.Elite)); // 최대 2를 넘지 않음
            Assert.AreEqual(8, _factory.Count(EnemyType.Normal));
        }

        [Test]
        public void EliteRollFail_SpawnsNoElite()
        {
            var spawner = CreateSpawner(eliteSpawnChance: 0.2f);
            _random.EliteRoll = 0.5f; // 0.5 < 0.2 거짓
            StartWave(enemyCount: 5, maxElite: 2);

            AdvanceUntilSpawned(spawner, 10);

            Assert.AreEqual(0, _factory.Count(EnemyType.Elite));
            Assert.AreEqual(10, _factory.Count(EnemyType.Normal));
        }

        [Test]
        public void EliteRoll_ExactlyChance_IsFail()
        {
            var spawner = CreateSpawner(eliteSpawnChance: 0.2f);
            _random.EliteRoll = 0.2f; // < 비교라 경계값은 실패
            StartWave(enemyCount: 1, maxElite: 1);

            AdvanceUntilSpawned(spawner, 1);

            Assert.AreEqual(EnemyType.Normal, _factory.Types[0]);
        }

        // ───────── Boss + Elite ─────────

        [Test]
        public void BossAndElite_BossFirst_ThenElite_ThenNormal()
        {
            var spawner = CreateSpawner(eliteSpawnChance: 1f);
            _random.EliteRoll = 0f;
            StartWave(enemyCount: 5, maxElite: 2, maxBoss: 1);

            AdvanceUntilSpawned(spawner, 5);

            CollectionAssert.AreEqual(
                new[] { EnemyType.Boss, EnemyType.Elite, EnemyType.Elite, EnemyType.Normal, EnemyType.Normal },
                _factory.Types);
        }

        // ───────── 버스트 수 제한 ─────────

        [Test]
        public void BossAndElite_BurstDoesNotExceedEnemyCount()
        {
            var spawner = CreateSpawner(eliteSpawnChance: 1f);
            _random.EliteRoll = 0f;
            StartWave(enemyCount: 2, maxElite: 1, maxBoss: 1);

            spawner.Advance(1f); // Boss
            spawner.Advance(1f); // Elite → 버스트 2마리 채움, 대기 상태
            CollectionAssert.AreEqual(new[] { EnemyType.Boss, EnemyType.Elite }, _factory.Types);

            spawner.Advance(1f);
            spawner.Advance(1f); // 쿨다운 중
            Assert.AreEqual(2, _factory.Types.Count); // 특수 몬스터가 있어도 enemyCount를 넘지 않음

            spawner.Advance(1f); // 누적 3초 → 쿨다운 끝 (스폰 X)
            spawner.Advance(1f); // 다음 버스트
            Assert.AreEqual(3, _factory.Types.Count);
            Assert.AreEqual(EnemyType.Normal, _factory.Types[2]);
        }

        [Test]
        public void SpecialsMoreThanEnemyCount_StillCappedPerBurst()
        {
            var spawner = CreateSpawner(eliteSpawnChance: 1f);
            _random.EliteRoll = 0f;
            StartWave(enemyCount: 2, maxElite: 3, maxBoss: 1); // 특수 4마리 > 버스트 2마리

            spawner.Advance(1f);
            spawner.Advance(1f);
            spawner.Advance(1f); // 쿨다운 중

            Assert.AreEqual(2, _factory.Types.Count);

            AdvanceUntilSpawned(spawner, 6);
            Assert.AreEqual(1, _factory.Count(EnemyType.Boss));
            Assert.AreEqual(3, _factory.Count(EnemyType.Elite));
            Assert.AreEqual(2, _factory.Count(EnemyType.Normal));
        }

        // ───────── 웨이브 전환 ─────────

        [Test]
        public void NextWave_ResetsRemainingCounts_NoCarryOver()
        {
            var spawner = CreateSpawner(eliteSpawnChance: 0.2f);
            _random.EliteRoll = 0.9f; // 1웨이브: 엘리트 판정 실패 → 엘리트 2 남음
            StartWave(enemyCount: 3, maxElite: 2);
            AdvanceUntilSpawned(spawner, 3);
            Assert.AreEqual(0, _factory.Count(EnemyType.Elite));

            _waveGaugeFilled.Publish(new WaveGaugeFilled(false));
            _random.EliteRoll = 0f; // 2웨이브: 판정은 성공하지만
            StartWave(enemyCount: 3, maxElite: 0); // 최대 0 → 1웨이브에서 남은 수가 넘어오면 안 됨
            AdvanceUntilSpawned(spawner, 6);

            Assert.AreEqual(0, _factory.Count(EnemyType.Elite));
        }

        [Test]
        public void EachBossWave_SpawnsItsOwnBoss()
        {
            var spawner = CreateSpawner(eliteSpawnChance: 0f);

            StartWave(enemyCount: 2, maxBoss: 1);
            AdvanceUntilSpawned(spawner, 2);

            _waveGaugeFilled.Publish(new WaveGaugeFilled(false));
            StartWave(enemyCount: 2, maxBoss: 1);
            AdvanceUntilSpawned(spawner, 4);

            CollectionAssert.AreEqual(
                new[] { EnemyType.Boss, EnemyType.Normal, EnemyType.Boss, EnemyType.Normal },
                _factory.Types);
        }
    }
}