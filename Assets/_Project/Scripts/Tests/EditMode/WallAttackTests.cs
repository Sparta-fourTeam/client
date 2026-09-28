using System;
using Game.Core.Defense;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class WallAttackTests
    {
        // 공격 간격이 1초인데 0.9초만 지났을 때 공격하지 않는지
        [Test]
        public void Tick_BeforeInterval_ReturnsZero()
        {
            var attack = new WallAttack(1f);

            int hits = attack.Tick(0.9f);

            Assert.AreEqual(0, hits);
        }

        // 1.2초가 쌓였을 때 정확히 1번 공격하는지
        [Test]
        public void Tick_AccumulatedTime_ReturnsOncePerInterval()
        {
            var attack = new WallAttack(1f);
            int totalHits = 0;

            for (int i = 0; i < 4; i++)
            {
                totalHits += attack.Tick(0.3f); // 누적 1.2초
            }

            Assert.AreEqual(1, totalHits);
        }

        // 한 프레임에 3.5초가 지나면 3번 공격하는지
        [Test]
        public void Tick_LongFrame_ReturnsMultipleHits()
        {
            var attack = new WallAttack(1f);

            int hits = attack.Tick(3.5f); // 프레임이 길게 튄 경우

            Assert.AreEqual(3, hits);
        }

        // 남은 시간이 다음 공격으로 넘어가는지 (3.5초 → 0.5초 남음 → 0.5초 더 지나면 1번)
        [Test]
        public void Tick_Remainder_CarriesOverToNextTick()
        {
            var attack = new WallAttack(1f);
            attack.Tick(3.5f);

            int hits = attack.Tick(0.5f);

            Assert.AreEqual(1, hits);
        }

        // deltaTime이 0이면(일시정지) 공격하지 않는지
        [Test]
        public void Tick_ZeroDeltaTime_ReturnsZero()
        {
            var attack = new WallAttack(1f);

            int hits = attack.Tick(0f); // timeScale 0 = 일시정지

            Assert.AreEqual(0, hits);
        }

        // 공격 간격이 0 이하면 예외가 나는지
        [TestCase(0f)]
        [TestCase(-1f)]
        public void Constructor_NonPositiveInterval_Throws(float interval)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new WallAttack(interval));
        }
    }
}
