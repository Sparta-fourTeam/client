using Game.Core;
using Game.View;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class LobbyNotificationTests
    {
        [TestCase(true, 1, "upgrade", 10, "material", 2, 10, 2, true)]
        [TestCase(false, 1, "upgrade", 10, "material", 2, 10, 2, false)]
        [TestCase(true, 2, "upgrade", 10, "material", 2, 10, 2, false)]
        [TestCase(true, 1, null, 0, null, 0, 10, 2, false)]
        [TestCase(true, 1, "", 0, null, 0, 10, 2, false)]
        [TestCase(true, 1, "upgrade", 10, "material", 2, 9, 2, false)]
        [TestCase(true, 1, "upgrade", 10, "material", 2, 10, 1, false)]
        [TestCase(true, 1, "upgrade", 10, null, 0, 10, 0, true)]
        [TestCase(true, 1, "upgrade", 10, null, 2, 10, 5, false)]
        public void SkillAndEquipmentUseTheSameActionableUpgradeRule(bool unlocked, int level, string id,
            int coinCost, string material, int materialCost, int gold, int owned, bool expected)
        {
            var profile = new PlayerProfile(new GameDataStore());
            profile.Apply(new PlayerSnapshot { gold = gold, items = new() { new() { itemId = "material", quantity = owned } } });
            Assert.That(LobbyAvailability.CanUpgrade(profile, new EquipInfo
            {
                IsUnlocked = unlocked,
                Level = level,
                MaxLevel = 2,
                UpgradeId = id,
                CoinCost = coinCost,
                MaterialItemId = material,
                MaterialCost = materialCost
            }), Is.EqualTo(expected));
            Assert.That(LobbyAvailability.CanUpgrade(profile, new SkillInfo
            {
                IsUnlocked = unlocked,
                Level = level,
                MaxLevel = 2,
                UpgradeId = id,
                CoinCost = coinCost,
                MaterialItemId = material,
                MaterialCost = materialCost
            }), Is.EqualTo(expected));
        }

        [Test]
        public void RewardBadgeIncludesEarlierStagesAndClearsOnlyAfterEveryRewardIsClaimed()
        {
            var profile = new PlayerProfile(new GameDataStore());
            profile.Apply(new PlayerSnapshot
            {
                stageProgress = new() {
                new() { stageId = 1, clearRating = 3, claimedRating = 2 },
                new() { stageId = 2, clearRating = 0, claimedRating = 0 }
            }
            });
            Assert.That(LobbyAvailability.HasAnyReward(profile), Is.True);
            profile.Apply(new PlayerSnapshot
            {
                stageProgress = new() {
                new() { stageId = 1, clearRating = 3, claimedRating = 3 },
                new() { stageId = 2, clearRating = 0, claimedRating = 0 }
            }
            });
            Assert.That(LobbyAvailability.HasAnyReward(profile), Is.False);
        }
    }
}
