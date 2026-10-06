using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Core.Messages;
using Game.Sandbox;
using MessagePipe;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Tests
{
    /// <summary>스킬 샌드박스의 적 필드: 샌드백, 이동 모드, 되살림, 대상 정렬, 피해 집계</summary>
    public sealed class SandboxEnemyFieldTests
    {
        // MessagePipe 없이 발행·구독을 이어 주는 최소한의 메시지 통로
        private sealed class Bus<T> : IPublisher<T>, ISubscriber<T>
        {
            private readonly List<IMessageHandler<T>> handlers = new List<IMessageHandler<T>>();
            public void Publish(T message) { foreach (var handler in handlers.ToList()) { handler.Handle(message); } }
            public IDisposable Subscribe(IMessageHandler<T> handler, params MessageHandlerFilter<T>[] filters)
            {
                handlers.Add(handler);
                return new Unsubscribe(() => handlers.Remove(handler));
            }
            private sealed class Unsubscribe : IDisposable
            {
                private readonly Action action;
                public Unsubscribe(Action action) => this.action = action;
                public void Dispose() => action();
            }
        }

        private GameObject root;
        private SandboxEnemyField field;
        private Bus<EnemyDied> died;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("SandboxEnemyFieldTest");
            field = root.AddComponent<SandboxEnemyField>();
            var hp = new Bus<EnemyHpChanged>();
            died = new Bus<EnemyDied>();
            field.Construct(hp, died, hp, died);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        [Test]
        public void StationaryDummies_DoNotMoveEvenWhenTicked()
        {
            var model = field.Spawn(new Vector2(1, 2), 100);
            field.Advance(1f);
            Assert.AreEqual(new Vector2(1, 2), model.Position);
        }

        [Test]
        public void MovingMode_WalksDownTowardTheWallAtSpeed()
        {
            field.Speed = 2f;
            field.Moving = true;
            var model = field.Spawn(new Vector2(0, 5), 100);
            field.Advance(1f);
            Assert.AreEqual(3f, model.Position.y, .001f);
            field.Moving = false;
            field.Advance(1f);
            Assert.AreEqual(3f, model.Position.y, .001f, "이동 모드를 끄면 다시 멈춘다");
        }

        [Test]
        public void StatusEffectsTickEvenOnStationaryDummies()
        {
            var model = field.Spawn(Vector2.zero, 1000);
            model.ApplyBurn(50, 3);
            field.Advance(1f);
            Assert.Less(model.Hp, 1000, "화상은 제자리 샌드백에도 걸린다");
        }

        [Test]
        public void GetNearest_SortsByDistanceSkipsDeadAndTrimsToCount()
        {
            var far = field.Spawn(new Vector2(9, 0), 100);
            var near = field.Spawn(new Vector2(1, 0), 100);
            var middle = field.Spawn(new Vector2(4, 0), 100);
            var results = new List<IEnemyTarget>();
            Assert.AreEqual(3, field.GetNearest(Vector2.zero, 10, results));
            CollectionAssert.AreEqual(new IEnemyTarget[] { near, middle, far }, results);
            Assert.AreEqual(2, field.GetNearest(Vector2.zero, 2, results));
            CollectionAssert.AreEqual(new IEnemyTarget[] { near, middle }, results);
            near.TakeDamage(1000);
            field.GetNearest(Vector2.zero, 10, results);
            CollectionAssert.DoesNotContain(results, near, "죽은 적은 대상이 아니다");
        }

        [Test]
        public void Respawn_RecreatesADeadDummyInTheSamePlaceWithFullHp()
        {
            field.Respawn = true;
            var first = field.Spawn(new Vector2(2, 3), 100);
            first.TakeDamage(100);
            field.Advance(.1f);
            Assert.AreEqual(1, field.AliveCount);
            var again = field.Models.Single();
            Assert.AreNotSame(first, again);
            Assert.AreEqual(new Vector2(2, 3), again.Position);
            Assert.AreEqual(100, again.Hp);
        }

        [Test]
        public void WithoutRespawn_DeadDummiesAreRemoved()
        {
            field.Respawn = false;
            var model = field.Spawn(Vector2.zero, 100);
            model.TakeDamage(100);
            field.Advance(.1f);
            Assert.AreEqual(0, field.AliveCount);
            Assert.AreEqual(0, field.Models.Count);
        }

        [Test]
        public void DamageIsCountedFromEveryHpDecrease()
        {
            var model = field.Spawn(Vector2.zero, 1000);
            model.TakeDamage(30);
            model.TakeDamage(20);
            Assert.AreEqual(50, field.TotalDamage);
            field.ResetDamage();
            Assert.AreEqual(0, field.TotalDamage);
            model.TakeDamage(5);
            Assert.AreEqual(5, field.TotalDamage);
        }

        [Test]
        public void Clear_RemovesEveryDummyAndDoesNotCountItAsDamage()
        {
            field.Spawn(new Vector2(0, 0), 100);
            field.Spawn(new Vector2(1, 0), 100);
            field.Clear();
            Assert.AreEqual(0, field.AliveCount);
            Assert.AreEqual(0, field.TotalDamage);
            var results = new List<IEnemyTarget>();
            Assert.AreEqual(0, field.GetNearest(Vector2.zero, 10, results));
        }

        [Test]
        public void SpawnPattern_CreatesRequestedCountAroundTheCenter()
        {
            field.SpawnPattern(SandboxEnemyField.Pattern.Line, 5, 100, new Vector2(0, 2));
            var xs = field.Models.Select(m => m.Position.x).OrderBy(x => x).ToList();
            Assert.AreEqual(5, xs.Count);
            Assert.AreEqual(-2f, xs[0], .001f);
            Assert.AreEqual(2f, xs[4], .001f);
            field.Clear();
            field.SpawnPattern(SandboxEnemyField.Pattern.Cluster, 7, 100, Vector2.zero);
            Assert.AreEqual(7, field.AliveCount);
            Assert.IsTrue(field.Models.All(m => m.Position.magnitude <= .9f + .001f));
        }

        [Test]
        public void RecentDps_ReflectsRecentDamageOnly()
        {
            var model = field.Spawn(Vector2.zero, 100000);
            model.TakeDamage(300);
            Assert.AreEqual(100f, field.RecentDps(3f), .001f, "300 피해 / 3초");
            field.ResetDamage();
            Assert.AreEqual(0f, field.RecentDps(3f));
        }
    }
}
