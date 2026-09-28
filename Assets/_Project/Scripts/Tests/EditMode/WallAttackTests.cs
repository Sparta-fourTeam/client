using System;
using Game.Core.Defense;
using NUnit.Framework;

namespace Game.Tests
{
    public class WallAttackTests
    {
        private sealed class FakeWall : IWall
        {
            public int CurrentHp { get; private set; } = 100;
            public int MaxHp => 100;
            public bool IsDestroyed { get; set; }
            public int HitCount { get; private set; }

            public void TakeDamage(int damage)
            {
                HitCount++;
                CurrentHp -= damage;
            }
        }

        //공격간격이 1초인데 0.9초만 지났을때 공격 안하는지
        [Test]
        public void Tick_BeforeInterval_DoesNotAttack()
        {
            var wall = new FakeWall();
            var attack = new WallAttack(wall, 5, 1f);

            attack.Tick(0.9f);

            Assert.AreEqual(0, wall.HitCount);
        }

        //1.2초가 지났을때 1번만 공격하는지
        [Test]
        public void Tick_AccumulatedTime_AttacksOncePerInterval()
        {
            var wall = new FakeWall();
            var attack = new WallAttack(wall, 5, 1f);

            for (int i = 0; i < 4; i++)
            {
                attack.Tick(0.3f); // 누적 1.2초
            }

            Assert.AreEqual(1, wall.HitCount);
            Assert.AreEqual(95, wall.CurrentHp);
        }

        //한 프레임에 3.5초가 지나면 3번 공격하는지
        [Test]
        public void Tick_LongFrame_AttacksMultipleTimes()
        {
            var wall = new FakeWall();
            var attack = new WallAttack(wall, 5, 1f);

            attack.Tick(3.5f); // 프레임이 길게 튄 경우

            Assert.AreEqual(3, wall.HitCount);
        }

        //벽이 파괴되었으면 공격 안함
        [Test]
        public void Tick_WallDestroyed_DoesNotAttack()
        {
            var wall = new FakeWall { IsDestroyed = true };
            var attack = new WallAttack(wall, 5, 1f);

            attack.Tick(5f);

            Assert.AreEqual(0, wall.HitCount);
        }

        //deltatime 0으로 했을때 공격 안함
        [Test]
        public void Tick_ZeroDeltaTime_DoesNotAttack()
        {
            var wall = new FakeWall();
            var attack = new WallAttack(wall, 5, 1f);

            attack.Tick(0f); // timeScale 0 = 일시정지

            Assert.AreEqual(0, wall.HitCount);
        }

        //공격 간격이 0 이하면 예외처리가 되는지
        [TestCase(0f)]
        [TestCase(-1f)]
        public void Constructor_NonPositiveInterval_Throws(float interval)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new WallAttack(new FakeWall(), 5, interval));
        }


    }
}
