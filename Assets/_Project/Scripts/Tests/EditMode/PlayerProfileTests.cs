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
                energyUpdatedAt = "2026-01-01T00:00:00",
                stageProgress = new(),
                upgrades = new() { new UpgradeRow { upgradeId = "atk", level = 2 } },
            });

            Assert.AreEqual(500, profile.Gold);
            Assert.AreEqual(10, profile.EnergyStored);
            Assert.AreEqual(2, profile.UpgradeLevel("atk"));
            Assert.AreEqual(0, profile.UpgradeLevel("unknown"));
        }

        [Test(Description = "clearRating이 claimedRating보다 높으면 그 구간의 RatingRewards를 합산해 지급한다")]
        public void ClaimRatingReward_GrantsSumOfUnclaimedTiers()
        {
            var profile = NewProfile();
            profile.Apply(new PlayerSnapshot
            {
                stageProgress = new() { new StageProgressRow { stageId = 1, clearRating = 2, claimedRating = 0 } },
                upgrades = new(),
            });

            var claimed = profile.ClaimRatingReward(1);

            Assert.AreEqual(70, claimed);
            Assert.AreEqual(70, profile.Gold);
        }

        [Test(Description = "이미 다 받은 보상을 다시 요청하면 NO_REWARD_TO_CLAIM으로 거절한다")]
        public void ClaimRatingReward_WhenAlreadyClaimed_ThrowsNoRewardToClaim()
        {
            var profile = NewProfile();
            profile.Apply(new PlayerSnapshot
            {
                stageProgress = new() { new StageProgressRow { stageId = 1, clearRating = 1, claimedRating = 1 } },
                upgrades = new(),
            });

            var ex = Assert.Throws<ApiException>(() => profile.ClaimRatingReward(1));

            Assert.AreEqual("NO_REWARD_TO_CLAIM", ex.Code);
        }

        [Test(Description = "진행 기록이 없는 스테이지를 요청하면 UNKNOWN_DATA_ID로 거절한다")]
        public void ClaimRatingReward_WhenStageProgressMissing_ThrowsUnknownDataId()
        {
            var profile = NewProfile();
            profile.Apply(new PlayerSnapshot { stageProgress = new(), upgrades = new() });

            var ex = Assert.Throws<ApiException>(() => profile.ClaimRatingReward(1));

            Assert.AreEqual("UNKNOWN_DATA_ID", ex.Code);
        }
    }
}
