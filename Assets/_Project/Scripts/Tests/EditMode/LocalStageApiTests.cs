using System;
using System.IO;
using Game.Core;
using Game.Network;
using NUnit.Framework;

namespace Game.Tests
{
    public class LocalStageApiTests
    {
        private string _filePath;
        private LocalSaveStore _store;
        private LocalStageApi _api;

        [SetUp]
        public void SetUp()
        {
            _filePath = Path.Combine(Path.GetTempPath(), $"local-stage-test-{Guid.NewGuid()}.json");
            _store = new LocalSaveStore(_filePath);
            _api = new LocalStageApi(_store, new GameDataStore());
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
            }
        }

        [Test(Description = "clearRating까지의 미수령 보상을 한 번에 지급하고 claimedRating을 갱신한다")]
        public void ClaimRatingReward_WithUnclaimedRewards_GrantsSumAndUpdatesClaimed()
        {
            var save = _store.Load();
            save.stageProgress[0].clearRating = 2;
            _store.Flush(save);

            // Stage 1 RatingRewards = [20, 50, 100] -> rating 1+2 = 20+50
            var snap = _api.ClaimRatingReward(1).GetAwaiter().GetResult();

            Assert.AreEqual(70, snap.gold);
            Assert.AreEqual(2, _store.Load().stageProgress[0].claimedRating);
        }

        [Test(Description = "받을 보상이 없으면 NO_REWARD로 거절한다")]
        public void ClaimRatingReward_WithNoUnclaimedReward_ThrowsNoReward()
        {
            var ex = Assert.Throws<ApiException>(() => _api.ClaimRatingReward(1).GetAwaiter().GetResult());

            Assert.AreEqual("NO_REWARD", ex.Code);
        }

        [Test(Description = "이미 수령한 rating을 다시 수령하려 하면 NO_REWARD로 거절한다(중복 지급 방지)")]
        public void ClaimRatingReward_CalledTwice_SecondCallThrowsNoReward()
        {
            var save = _store.Load();
            save.stageProgress[0].clearRating = 1;
            _store.Flush(save);
            _api.ClaimRatingReward(1).GetAwaiter().GetResult();

            var ex = Assert.Throws<ApiException>(() => _api.ClaimRatingReward(1).GetAwaiter().GetResult());

            Assert.AreEqual("NO_REWARD", ex.Code);
        }

        [Test(Description = "존재하지 않는 스테이지 진행도면 INVALID_ID로 거절한다")]
        public void ClaimRatingReward_UnknownStage_ThrowsInvalidId()
        {
            var ex = Assert.Throws<ApiException>(() => _api.ClaimRatingReward(999).GetAwaiter().GetResult());

            Assert.AreEqual("INVALID_ID", ex.Code);
        }
    }
}
