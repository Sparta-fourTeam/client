using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core.Defense;
using Game.Core.Messages;
using MessagePipe;
using NUnit.Framework;
using UnityEngine;

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
        private GameObject _go;

        [SetUp]
        public void SetUp()
        {
            _hp = new FakePublisher<WallHpChanged>();
            _destroyed = new FakePublisher<WallDestroyed>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
            {
                UnityEngine.Object.DestroyImmediate(_go);
            }
        }

        // 주입만 되고 Initialize 전인 벽
        private Wall CreateUninitializedWall()
        {
            _go = new GameObject("Wall");
            Wall wall = _go.AddComponent<Wall>();
            wall.Construct(_hp, _destroyed, null); // EditMode에서는 Start가 호출되지 않아 스테이지 값을 쓰지 않는다
            return wall;
        }

        private Wall CreateWall(int maxHp)
        {
            Wall wall = CreateUninitializedWall();
            wall.Initialize(maxHp);
            return wall;
        }

        [Test(Description = "초기화 하면 초기 HP를 1번 발행한다")]
        public void Initialize_PublishesInitialHp()
        {
            CreateWall(10);

            Assert.AreEqual(1, _hp.Published.Count);
            Assert.AreEqual(10, _hp.Published[0].Current);
            Assert.AreEqual(10, _hp.Published[0].Max);
        }

        [Description("최대 HP가 0 이하면 예외가 난다")]
        [TestCase(0)]
        [TestCase(-1)]
        public void Initialize_NonPositiveMaxHp_Throws(int maxHp)
        {
            Wall wall = CreateUninitializedWall();

            Assert.Throws<ArgumentOutOfRangeException>(() => wall.Initialize(maxHp));
        }

        [Test(Description = "초기화 전에 공격받으면 무시한다")]
        public void TakeDamage_BeforeInitialize_IsIgnored()
        {
            Wall wall = CreateUninitializedWall();

            wall.TakeDamage(5);

            Assert.IsFalse(wall.IsDestroyed);
            Assert.AreEqual(0, _hp.Published.Count);
            Assert.AreEqual(0, _destroyed.Published.Count);
        }

        [Test(Description = "수리하면 체력이 늘고 HUD에 보내는 값도 늘어난다")]
        public void Repair_RestoresHpAndPublishes()
        {
            Wall wall = CreateWall(100);
            wall.TakeDamage(60);

            wall.Repair(20);

            Assert.AreEqual(60, wall.CurrentHp);
            Assert.AreEqual(60, _hp.Published.Last().Current);

            wall.Repair(100);
            Assert.AreEqual(100, wall.CurrentHp);
            Assert.AreEqual(100, _hp.Published.Last().Current);
        }

        [Test(Description = "부서진 방벽, 가득 찬 방벽, 0 이하 수리량은 무시한다")]
        public void Repair_IgnoredWhenDestroyedFullOrNonPositive()
        {
            Wall wall = CreateWall(100);
            int published = _hp.Published.Count;
            wall.Repair(10); // 가득 찬 방벽
            Assert.AreEqual(published, _hp.Published.Count);

            wall.TakeDamage(10);
            published = _hp.Published.Count;
            wall.Repair(0);
            wall.Repair(-5);
            Assert.AreEqual(90, wall.CurrentHp);
            Assert.AreEqual(published, _hp.Published.Count);

            wall.TakeDamage(999);
            wall.Repair(50); // 부서진 방벽
            Assert.AreEqual(0, wall.CurrentHp);
            Assert.IsTrue(wall.IsDestroyed);
        }

        [Description("0이나 음수 데미지는 무시한다")]
        [TestCase(0)]
        [TestCase(-5)]
        public void TakeDamage_NonPositiveAmount_IsIgnored(int amount)
        {
            Wall wall = CreateWall(10);

            wall.TakeDamage(amount);

            Assert.AreEqual(10, wall.CurrentHp);
            Assert.AreEqual(1, _hp.Published.Count); // Initialize에서 발행한 초기값만
        }

        [Test(Description = "과잉 피해는 HP를 0으로 제한하고, 파괴 이후 추가 HP/파괴 메시지를 발행하지 않는다")]
        public void TakeDamage_ReachesZero_PublishesDestroyedOnce()
        {
            Wall wall = CreateWall(10);
            wall.TakeDamage(999);
            Assert.AreEqual(0, wall.CurrentHp);
            Assert.AreEqual(0, _hp.Published.Last().Current);
            Assert.IsTrue(wall.IsDestroyed);
            Assert.AreEqual(1, _destroyed.Published.Count);
            int hpCountAfterDestroy = _hp.Published.Count;

            wall.TakeDamage(10);
            wall.TakeDamage(1);
            Assert.AreEqual(hpCountAfterDestroy, _hp.Published.Count);
            Assert.AreEqual(1, _destroyed.Published.Count);
        }

        [Test(Description = "파괴된 벽을 다시 초기화 하면 새 판처럼 HP가 채워지고 파괴상태가 풀린다")]
        public void Initialize_AfterDestroyed_ResetsWall()
        {
            Wall wall = CreateWall(10);
            wall.TakeDamage(10);
            Assert.IsTrue(wall.IsDestroyed);
            Assert.AreEqual(1, _destroyed.Published.Count);

            wall.Initialize(20);

            Assert.IsFalse(wall.IsDestroyed);
            Assert.AreEqual(20, wall.CurrentHp);
            Assert.AreEqual(20, _hp.Published.Last().Current);
        }

    }
}
