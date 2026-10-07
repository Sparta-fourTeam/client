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
    // 슬라임 분열: SpawnPassive(OnDeath)와 EnemySpawner가 분열체를 만드는 흐름
    public sealed class EnemySplitTests
    {
        private const int ChildMonsterId = 99;

        private static readonly EnemyAttackStats Attack = new EnemyAttackStats(AttackType.Melee, 1, 1f, 0f);

        private sealed class Recorder<T> : IPublisher<T>
        {
            public readonly List<T> Messages = new();
            public void Publish(T message) => Messages.Add(message);
        }

        private sealed class FakeWallPublisher<T> : IPublisher<T>, IBufferedPublisher<T>
        {
            public void Publish(T message) { }
        }

        private sealed class FakeSubscriber<T> : ISubscriber<T>
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

            private sealed class Subscription : IDisposable
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

        private sealed class FixedRandom : IRandomProvider
        {
            public float Range(float min, float max) => 1f;
        }

        // 일반 스폰은 분열 패시브를 가진 적, Id 지정 생성은 분열체(isSummoned)를 만든다
        private sealed class SplitFactory : IEnemyFactory
        {
            private int _nextId;

            public readonly Recorder<EnemyHpChanged> HpChanged = new();
            public readonly Recorder<EnemyDied> Died = new();
            public readonly List<Enemy> Parents = new();
            public readonly List<Enemy> Children = new();
            public readonly List<int> RequestedIds = new();

            public Enemy Create(int monsterId, Vector2 spawnPosition, bool isSummoned = false)
            {
                if (!isSummoned)
                {
                    var parent = TestEnemy.Create(new EnemyModel(++_nextId, 0f, EnemyType.Normal, 10, Attack, HpChanged, Died,
                        new IPassive[] { new SpawnPassive(SpawnTrigger.OnDeath, ChildMonsterId, 2) }), spawnPosition);
                    Parents.Add(parent);
                    return parent;
                }

                RequestedIds.Add(monsterId);
                var child = TestEnemy.Create(new EnemyModel(++_nextId, 0f, EnemyType.Normal, 10, Attack, HpChanged, Died,
                    null, isSummoned: true), spawnPosition);
                Children.Add(child);
                return child;
            }
        }

        private readonly List<GameObject> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _created)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }
            _created.Clear();
        }

        private static Enemy CreateSplitter(Vector2 position, int count = 2, EnemyType type = EnemyType.Normal)
        {
            var model = new EnemyModel(1, 0f, type, 10, Attack,
                new Recorder<EnemyHpChanged>(), new Recorder<EnemyDied>(),
                new IPassive[] { new SpawnPassive(SpawnTrigger.OnDeath, ChildMonsterId, count) });
            return TestEnemy.Create(model, position);
        }

        private EnemySpawner CreateSpawner(SplitFactory factory, out FakeSubscriber<WaveStarted> waveStarted,
            out Recorder<AllEnemiesCleared> allCleared)
        {
            var wallObject = new GameObject("Wall");
            _created.Add(wallObject);
            wallObject.transform.position = new Vector3(0f, -100f, 0f); // 적이 사거리에 들어가지 않게 멀리
            var wall = wallObject.AddComponent<Wall>();
            wall.Construct(new FakeWallPublisher<WallHpChanged>(), new FakeWallPublisher<WallDestroyed>(), null);
            wall.Initialize(100);

            var areaObject = new GameObject("SpawnArea");
            _created.Add(areaObject);
            var area = areaObject.AddComponent<SpawnArea>();

            waveStarted = new FakeSubscriber<WaveStarted>();
            allCleared = new Recorder<AllEnemiesCleared>();

            var spawner = new EnemySpawner(
                factory,
                new EnemyRoster(new[] { new MonsterDefinition { Id = 1, Hp = 10, AttackInterval = 1f } }),
                new EnemySpawnConfig(spawnIntervalMin: 0.1f, spawnIntervalMax: 0.5f, spawnCooldown: 3f),
                area,
                new FixedRandom(),
                waveStarted,
                allCleared,
                wall,
                new EnemyProjectileSystem());
            spawner.Initialize();
            return spawner;
        }

        // ───────── SpawnPassive (OnDeath) ─────────

        [Test(Description = "치명상이 아닌 피해에는 분열을 요청하지 않는다")]
        public void NonLethalDamage_DoesNotRequestSpawn()
        {
            var enemy = CreateSplitter(Vector2.zero);
            var requests = new List<EnemySpawnRequest>();
            enemy.SpawnRequested += requests.Add;

            enemy.TakeDamage(5);

            Assert.IsEmpty(requests);
        }

        [Test(Description = "죽는 순간 지정한 몬스터를 count마리, 죽은 자리에 가로로 벌려 요청한다")]
        public void LethalDamage_RequestsChildrenAroundDeathPosition()
        {
            var enemy = CreateSplitter(new Vector2(3f, 5f), count: 2);
            var requests = new List<EnemySpawnRequest>();
            enemy.SpawnRequested += requests.Add;

            enemy.TakeDamage(10);

            Assert.AreEqual(2, requests.Count);
            Assert.AreEqual(ChildMonsterId, requests[0].MonsterId);
            Assert.AreEqual(ChildMonsterId, requests[1].MonsterId);
            Assert.AreEqual(2.8f, requests[0].Position.x, 0.0001f);
            Assert.AreEqual(3.2f, requests[1].Position.x, 0.0001f);
            Assert.AreEqual(5f, requests[0].Position.y, 0.0001f);
            Assert.AreEqual(5f, requests[1].Position.y, 0.0001f);
        }

        [Test(Description = "이미 죽은 적에게 다시 피해가 와도 분열은 한 번만 요청한다")]
        public void DamageAfterDeath_DoesNotRequestAgain()
        {
            var enemy = CreateSplitter(Vector2.zero);
            var requests = new List<EnemySpawnRequest>();
            enemy.SpawnRequested += requests.Add;

            enemy.TakeDamage(10);
            enemy.TakeDamage(10);

            Assert.AreEqual(2, requests.Count);
        }

        [Test(Description = "분열 수는 1 이상이어야 한다")]
        public void SpawnPassive_RejectsNonPositiveCount()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SpawnPassive(SpawnTrigger.OnDeath, ChildMonsterId, 0));
        }

        [Test(Description = "일반 적의 사망 메시지는 웨이브에 세고, 분열체는 세지 않는다")]
        public void DiedMessage_MarksSummonedEnemies()
        {
            var died = new Recorder<EnemyDied>();
            var normal = new EnemyModel(1, 0f, EnemyType.Normal, 1, Attack, new Recorder<EnemyHpChanged>(), died);
            var summoned = new EnemyModel(2, 0f, EnemyType.Normal, 1, Attack, new Recorder<EnemyHpChanged>(), died,
                null, isSummoned: true);

            normal.TakeDamage(1);
            summoned.TakeDamage(1);

            Assert.IsFalse(died.Messages[0].IsSummoned);
            Assert.IsTrue(died.Messages[1].IsSummoned);
        }

        // ───────── EnemySpawner ─────────

        [Test(Description = "부모가 죽으면 다음 Advance에서 분열체를 만들어 필드에 올린다")]
        public void Spawner_CreatesChildrenOnNextAdvance()
        {
            var factory = new SplitFactory();
            var spawner = CreateSpawner(factory, out var waveStarted, out _);
            waveStarted.Publish(new WaveStarted(waveIndex: 1, enemyCount: 1, isFinalWave: true));
            spawner.Advance(1f);
            Assert.AreEqual(1, factory.Parents.Count); // 준비 확인

            factory.Parents[0].TakeDamage(10);

            Assert.AreEqual(0, factory.Children.Count); // 죽는 순간에는 목록을 건드리지 않는다
            spawner.Advance(0.1f);
            Assert.AreEqual(2, factory.Children.Count);
            CollectionAssert.AreEqual(new[] { ChildMonsterId, ChildMonsterId }, factory.RequestedIds);
        }

        [Test(Description = "분열체가 남아 있는 동안에는 클리어를 발행하지 않는다")]
        public void Spawner_DoesNotClearWhileChildrenAlive()
        {
            var factory = new SplitFactory();
            var spawner = CreateSpawner(factory, out var waveStarted, out var allCleared);
            waveStarted.Publish(new WaveStarted(waveIndex: 1, enemyCount: 1, isFinalWave: true));
            spawner.Advance(1f);

            factory.Parents[0].TakeDamage(10);
            spawner.Advance(0.1f);
            Assert.IsEmpty(allCleared.Messages);

            foreach (var child in factory.Children)
            {
                child.TakeDamage(10);
            }
            spawner.Advance(0.1f);

            Assert.AreEqual(1, allCleared.Messages.Count);
        }

        [Test(Description = "죽는 프레임에 TickCombat 안에서 발생한 분열도 클리어보다 먼저 처리된다")]
        public void Spawner_PendingSpawnBlocksClearInSameAdvance()
        {
            var factory = new SplitFactory();
            var spawner = CreateSpawner(factory, out var waveStarted, out var allCleared);
            waveStarted.Publish(new WaveStarted(waveIndex: 1, enemyCount: 1, isFinalWave: true));
            spawner.Advance(1f);

            // 화상 피해가 TickCombat 안에서 부모를 죽이는 경우
            factory.Parents[0].ApplyBurn(damagePerSecond: 10, duration: 5f);
            spawner.Advance(1f); // TickStatus가 1초 경과로 피해를 줘 죽는다

            Assert.IsEmpty(allCleared.Messages);
            spawner.Advance(0.1f);
            Assert.AreEqual(2, factory.Children.Count);
            Assert.IsEmpty(allCleared.Messages);
        }
    }
}
