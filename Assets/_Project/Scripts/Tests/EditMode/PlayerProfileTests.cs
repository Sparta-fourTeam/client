using System;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    public class PlayerProfileTests
    {
        private static PlayerProfile NewProfile() => new PlayerProfile(new GameDataStore());

        [Test(Description = "Apply 후에는 스냅샷 값을 그대로 노출한다")]
        public void Apply_ExposesSnapshotValues()
        {
            var profile = NewProfile();

            profile.Apply(new PlayerSnapshot
            {
                gold = 500,
                energyStored = 10,
                energyUpdatedAt = "2026-01-01T00:00:00Z",
                stageProgress = new(),
                upgrades = new() { new UpgradeRow { upgradeId = "atk", level = 2 } },
            });

            Assert.AreEqual(500, profile.Gold);
            Assert.AreEqual(10, profile.EnergyStored);
            Assert.AreEqual(2, profile.UpgradeLevel("atk"));
            Assert.AreEqual(0, profile.UpgradeLevel("unknown"));
        }

        [Test(Description = "energyUpdatedAt은 UTC 문자열을 시간대 변환 없이 그대로 UTC로 해석한다")]
        public void EnergyUpdatedAt_ParsesAsUtc()
        {
            var profile = NewProfile();
            profile.Apply(new PlayerSnapshot
            {
                energyUpdatedAt = "2026-01-01T00:00:00Z",
                stageProgress = new(),
                upgrades = new(),
            });

            var updatedAt = profile.EnergyUpdatedAt;

            Assert.AreEqual(DateTimeKind.Utc, updatedAt.Kind);
            Assert.AreEqual(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), updatedAt);
        }

        [Test(Description = "Apply 전에는 energyUpdatedAt이 비어 있어 예외 대신 DateTime.UtcNow를 반환한다")]
        public void EnergyUpdatedAt_BeforeApply_ReturnsUtcNow()
        {
            var profile = NewProfile();

            var updatedAt = profile.EnergyUpdatedAt;

            Assert.LessOrEqual((DateTime.UtcNow - updatedAt).TotalSeconds, 1);
        }

        [Test(Description = "stageProgress/upgrades가 null로 와도 빈 리스트로 정규화해 NRE 없이 조회한다")]
        public void Apply_WhenListsAreNull_NormalizesToEmpty()
        {
            var profile = NewProfile();

            profile.Apply(new PlayerSnapshot { stageProgress = null, upgrades = null, items = null });

            Assert.AreEqual(0, profile.UpgradeLevel("atk"));
            Assert.IsFalse(profile.IsStageCleared(1));
            Assert.AreEqual(0, profile.Gold);
            Assert.AreEqual(0, profile.EnergyStored);
            Assert.AreEqual(0, profile.ItemQuantity("book.arrow"));
        }
    }
}
