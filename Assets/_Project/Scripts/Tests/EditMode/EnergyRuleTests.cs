using System;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    public class EnergyRuleTests
    {
        private static readonly EnergyConfig Config = new EnergyConfig { Max = 100, RegenSeconds = 60 };

        [Test(Description = "이미 최대치면 회복 없이 그대로 유지된다")]
        public void At_WhenAlreadyAtMax_ReturnsMaxWithNoWait()
        {
            var now = DateTime.UtcNow;

            var (current, nextIn) = EnergyRule.At(100, now, now, Config);

            Assert.AreEqual(100, current);
            Assert.AreEqual(0, nextIn);
        }

        [Test(Description = "경과 시간만큼 자연 회복되고 다음 회복까지 남은 시간을 계산한다")]
        public void At_WhenTimeElapsed_RegeneratesProportionally()
        {
            var updatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var now = updatedAt.AddSeconds(150);

            var (current, nextIn) = EnergyRule.At(0, updatedAt, now, Config);

            Assert.AreEqual(2, current);
            Assert.AreEqual(30f, nextIn, 0.01f);
        }

        [Test(Description = "회복량이 최대치를 넘으면 최대치로 캡핑된다")]
        public void At_WhenRegenExceedsMax_CapsAtMax()
        {
            var updatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var now = updatedAt.AddHours(1);

            var (current, nextIn) = EnergyRule.At(90, updatedAt, now, Config);

            Assert.AreEqual(100, current);
            Assert.AreEqual(0, nextIn);
        }

        [Test(Description = "updatedAt이 미래 시각이어도(기기 시계 오차) 에너지가 줄어들지 않는다")]
        public void At_WhenUpdatedAtIsInFuture_DoesNotDecreaseEnergy()
        {
            var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var updatedAt = now.AddSeconds(150);

            var (current, nextIn) = EnergyRule.At(50, updatedAt, now, Config);

            Assert.AreEqual(50, current);
            Assert.AreEqual(60f, nextIn, 0.01f);
        }
    }
}
