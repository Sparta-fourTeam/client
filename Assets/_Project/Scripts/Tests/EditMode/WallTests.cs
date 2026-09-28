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
            wall.Construct(_hp, _destroyed);
            return wall;
        }

        private Wall CreateWall(int maxHp)
        {
            Wall wall = CreateUninitializedWall();
            wall.Initialize(maxHp);
            return wall;
        }

        // 초기화하면 초기 HP(10/10)를 1번 발행하는지 (늦게 뜬 HUD도 첫 피격 전부터 HP를 표시할 수 있게)
        [Test]
        public void Initialize_PublishesInitialHp()
        {
            CreateWall(10);

            Assert.AreEqual(1, _hp.Published.Count);
            Assert.AreEqual(10, _hp.Published[0].Current);
            Assert.AreEqual(10, _hp.Published[0].Max);
        }

        // 최대 HP가 0 이하면 예외가 나는지 (데이터 실수로 시작하자마자 실패 판정이 나는 것을 막기 위해)
        [TestCase(0)]
        [TestCase(-1)]
        public void Initialize_NonPositiveMaxHp_Throws(int maxHp)
        {
            Wall wall = CreateUninitializedWall();

            Assert.Throws<ArgumentOutOfRangeException>(() => wall.Initialize(maxHp));
        }

        // 초기화 전에 공격받으면 무시하는지 (HP 0 상태라 한 대 맞고 바로 파괴되면 안 됨)
        [Test]
        public void TakeDamage_BeforeInitialize_IsIgnored()
        {
            Wall wall = CreateUninitializedWall();

            wall.TakeDamage(5);

            Assert.IsFalse(wall.IsDestroyed);
            Assert.AreEqual(0, _hp.Published.Count);
            Assert.AreEqual(0, _destroyed.Published.Count);
        }

        // HP 10에서 3을 맞으면 7이 되고, HUD에 보내는 값도 7인지
        [Test]
        public void TakeDamage_ReducesHpByAmount()
        {
            Wall wall = CreateWall(10);

            wall.TakeDamage(3);

            Assert.AreEqual(7, wall.CurrentHp);
            Assert.AreEqual(7, _hp.Published.Last().Current);
            Assert.IsFalse(wall.IsDestroyed);
        }

        // HP보다 큰 데미지(999)를 맞아도 음수가 아니라 0이 되는지
        [Test]
        public void TakeDamage_OverMaxHp_ClampsToZero()
        {
            Wall wall = CreateWall(10);

            wall.TakeDamage(999);

            Assert.AreEqual(0, wall.CurrentHp);
            Assert.AreEqual(0, _hp.Published.Last().Current);
        }

        // 0이나 음수 데미지는 무시하는지 (음수 데미지로 벽이 회복되거나 HUD가 쓸데없이 갱신되면 안 됨)
        [TestCase(0)]
        [TestCase(-5)]
        public void TakeDamage_NonPositiveAmount_IsIgnored(int amount)
        {
            Wall wall = CreateWall(10);

            wall.TakeDamage(amount);

            Assert.AreEqual(10, wall.CurrentHp);
            Assert.AreEqual(1, _hp.Published.Count); // Initialize에서 발행한 초기값만
        }

        // HP가 0이 된 뒤 또 맞아도 파괴 메시지는 1번만 나가는지 (실패 판정·결과 전송은 판당 1번이어야 함)
        [Test]
        public void TakeDamage_ReachesZero_PublishesDestroyedOnce()
        {
            Wall wall = CreateWall(10);

            wall.TakeDamage(10);
            wall.TakeDamage(10);

            Assert.IsTrue(wall.IsDestroyed);
            Assert.AreEqual(1, _destroyed.Published.Count);
        }

        // 파괴된 뒤 남은 적이 계속 때려도 HP·파괴 메시지가 더 나가지 않는지
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

        // 여러 번 맞다가(4 + 4 + 4) 마지막 한 방이 남은 HP(2)보다 커도 0이 되며 파괴되는지
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

        // 파괴된 벽을 다시 초기화하면 새 판처럼 HP가 채워지고 파괴 상태가 풀리는지
        [Test]
        public void Initialize_AfterDestroyed_ResetsWall()
        {
            Wall wall = CreateWall(10);
            wall.TakeDamage(10);

            wall.Initialize(20);

            Assert.IsFalse(wall.IsDestroyed);
            Assert.AreEqual(20, wall.CurrentHp);
            Assert.AreEqual(20, _hp.Published.Last().Current);
        }
    }
}
