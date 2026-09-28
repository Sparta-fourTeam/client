using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core.Defense;
using Game.Core.Messages;
using MessagePipe;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class WallTests
    {
        private sealed class FakePublisher<T> : IPublisher<T>, IBufferedPublisher<T>
        {
            public List<T> Published { get; } = new List<T>();

            public void Publish(T message)
            {
                Published.Add(message);
            }
        }

        private FakePublisher<WallHpChanged> _hp;
        private FakePublisher<WallDestroyed> _destroyed;

        [SetUp]
        public void SetUp()
        {
            _hp = new FakePublisher<WallHpChanged>();
            _destroyed = new FakePublisher<WallDestroyed>();
        }

        private Wall CreateWall(int maxHp)
        {
            return new Wall(maxHp, _hp, _destroyed);
        }
        [Test]
        public void Constructor_PublishesInitialHp()
        {
            CreateWall(10);

            Assert.AreEqual(1, _hp.Published.Count);
            Assert.AreEqual(10, _hp.Published[0].Current);
            Assert.AreEqual(10, _hp.Published[0].Max);
        }
        [TestCase(0)]
        [TestCase(-1)]
        public void Constructor_NonPositiveMaxHp_Throws(int maxHp)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateWall(maxHp));
        }

        [Test]
        public void TakeDamage_ReducesHpByAmount()
        {
            Wall wall = CreateWall(10);

            wall.TakeDamage(3);

            Assert.AreEqual(7, wall.CurrentHp);
            Assert.AreEqual(7, _hp.Published.Last().Current);
            Assert.IsFalse(wall.IsDestroyed);
        }

        [Test]
        public void TakeDamage_OverMaxHp_ClampsToZero()
        {
            Wall wall = CreateWall(10);

            wall.TakeDamage(999);

            Assert.AreEqual(0, wall.CurrentHp);
            Assert.AreEqual(0, _hp.Published.Last().Current);
        }

        [TestCase(0)]
        [TestCase(-5)]
        public void TakeDamage_NonPositiveAmount_IsIgnored(int amount)
        {
            Wall wall = CreateWall(10);

            wall.TakeDamage(amount);

            Assert.AreEqual(10, wall.CurrentHp);
            Assert.AreEqual(1, _hp.Published.Count); // 생성자에서 발행한 초기값만
        }

        [Test]
        public void TakeDamage_ReachesZero_PublishesDestroyedOnce()
        {
            Wall wall = CreateWall(10);

            wall.TakeDamage(10);
            wall.TakeDamage(10);

            Assert.IsTrue(wall.IsDestroyed);
            Assert.AreEqual(1, _destroyed.Published.Count);
        }

        [Test]
        public void TakeDamage_AfterDestroyed_PublishesNothing()
        {
            Wall wall = CreateWall(10);
            wall.TakeDamage(10);
            int hpCountAfterDestroy = _hp.Published.Count;

            wall.TakeDamage(1);

            Assert.AreEqual(hpCountAfterDestroy, _hp.Published.Count);
            Assert.AreEqual(1, _destroyed.Published.Count);
        }

        [Test]
        public void TakeDamage_MultipleHits_AccumulatesUntilDestroyed()
        {
            Wall wall = CreateWall(10);

            wall.TakeDamage(4);
            wall.TakeDamage(4);
            Assert.IsFalse(wall.IsDestroyed);

            wall.TakeDamage(4);

            Assert.AreEqual(0, wall.CurrentHp);
            Assert.IsTrue(wall.IsDestroyed);
            Assert.AreEqual(1, _destroyed.Published.Count);
        }
    }
}
