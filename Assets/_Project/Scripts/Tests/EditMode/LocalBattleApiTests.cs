using System;
using System.IO;
using Game.Core;
using Game.Network;
using NUnit.Framework;

namespace Game.Tests
{
    public class LocalBattleApiTests
    {
        private string _filePath;
        private LocalSaveStore _store;
        private LocalBattleApi _api;

        [SetUp]
        public void SetUp()
        {
            _filePath = Path.Combine(Path.GetTempPath(), $"local-battle-test-{Guid.NewGuid()}.json");
            _store = new LocalSaveStore(_filePath);
            _api = new LocalBattleApi(_store, new GameDataStore());
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
            }
        }

        private void SeedEnergy(int energy)
        {
            var save = _store.Load();
            save.wallet.energyStored = energy;
            save.wallet.energyUpdatedAt = DateTime.UtcNow.ToString("O");
            _store.Flush(save);
        }

        [Test(Description = "에너지가 충분하면 전투를 발급하고 입장 비용만큼 에너지를 차감한다")]
        public void StartBattle_WithEnoughEnergy_IssuesBattleAndConsumesEnergy()
        {
            SeedEnergy(10);

            var response = _api.StartBattle(1, 1).GetAwaiter().GetResult();

            Assert.IsNotEmpty(response.battleId);
            var save = _store.Load();
            Assert.AreEqual(5, save.wallet.energyStored); // Energy.Cost = 5
            Assert.AreEqual(1, save.battles.Count);
            Assert.AreEqual("Issued", save.battles[0].status);
        }

        [Test(Description = "에너지가 부족하면 INSUFFICIENT_ENERGY로 거절한다")]
        public void StartBattle_WithoutEnoughEnergy_ThrowsInsufficientEnergy()
        {
            SeedEnergy(0);

            var ex = Assert.Throws<ApiException>(() => _api.StartBattle(1, 1).GetAwaiter().GetResult());

            Assert.AreEqual("INSUFFICIENT_ENERGY", ex.Code);
        }

        [Test(Description = "진행 기록이 없는 스테이지는 STAGE_LOCKED로 거절한다")]
        public void StartBattle_StageWithoutProgress_ThrowsStageLocked()
        {
            SeedEnergy(10);

            var ex = Assert.Throws<ApiException>(() => _api.StartBattle(2, 1).GetAwaiter().GetResult());

            Assert.AreEqual("STAGE_LOCKED", ex.Code);
        }

        [Test(Description = "마지막 스테이지를 클리어해도 테이블에 없는 다음 스테이지의 진행 기록은 만들지 않는다")]
        public void SubmitResult_ClearingLastStage_DoesNotCreateProgressForMissingNextStage()
        {
            SeedEnergy(10);
            var save = _store.Load();
            save.stageProgress.Add(new StageProgressRow { stageId = 3, clearRating = 0 });
            _store.Flush(save);
            var start = _api.StartBattle(3, 1).GetAwaiter().GetResult();

            _api.SubmitResult(new SubmitResultRequest { battleId = start.battleId, cleared = true, wallHpPercent = 100 })
                .GetAwaiter().GetResult();

            Assert.IsFalse(_store.Load().stageProgress.Exists(p => p.stageId == 4));
        }

        [Test(Description = "존재하지 않는 battleId로 결과를 제출하면 INVALID_ID로 거절한다")]
        public void SubmitResult_UnknownBattleId_ThrowsInvalidId()
        {
            var ex = Assert.Throws<ApiException>(() =>
                _api.SubmitResult(new SubmitResultRequest { battleId = "unknown" }).GetAwaiter().GetResult());

            Assert.AreEqual("INVALID_ID", ex.Code);
        }

        [Test(Description = "벽 체력 100%로 클리어하면 ClearGold를 지급하고 rating 3, 다음 스테이지 진행도를 만든다")]
        public void SubmitResult_ClearedAtFullWallHp_GrantsGoldAndTopRatingAndUnlocksNextStage()
        {
            SeedEnergy(10);
            var start = _api.StartBattle(1, 1).GetAwaiter().GetResult();

            var result = _api.SubmitResult(new SubmitResultRequest { battleId = start.battleId, cleared = true, wallHpPercent = 100 })
                .GetAwaiter().GetResult();

            Assert.IsTrue(result.cleared);
            Assert.AreEqual(100, result.rewardGold); // Stage 1 ClearGold
            var save = _store.Load();
            Assert.AreEqual(100, save.wallet.gold);
            Assert.AreEqual(3, save.stageProgress.Find(p => p.stageId == 1).clearRating);
            Assert.IsTrue(save.stageProgress.Exists(p => p.stageId == 2));
        }

        [Test(Description = "실패 제출은 골드를 지급하지 않고 clearRating도 바꾸지 않는다")]
        public void SubmitResult_Failed_GrantsNoGoldAndKeepsRating()
        {
            SeedEnergy(10);
            var start = _api.StartBattle(1, 1).GetAwaiter().GetResult();

            var result = _api.SubmitResult(new SubmitResultRequest { battleId = start.battleId, cleared = false })
                .GetAwaiter().GetResult();

            Assert.IsFalse(result.cleared);
            Assert.AreEqual(0, result.rewardGold);
            Assert.AreEqual(0, _store.Load().stageProgress.Find(p => p.stageId == 1).clearRating);
        }

        [Test(Description = "같은 battleId로 두 번 제출하면 처음 확정된 결과를 그대로 재반환하고 중복 지급하지 않는다")]
        public void SubmitResult_SubmittedTwice_ReturnsStoredResultWithoutDoublePay()
        {
            SeedEnergy(10);
            var start = _api.StartBattle(1, 1).GetAwaiter().GetResult();
            var req = new SubmitResultRequest { battleId = start.battleId, cleared = true, wallHpPercent = 100 };
            _api.SubmitResult(req).GetAwaiter().GetResult();

            var second = _api.SubmitResult(req).GetAwaiter().GetResult();

            Assert.AreEqual(100, second.rewardGold);
            Assert.AreEqual(100, _store.Load().wallet.gold); // 두 번 지급되지 않음
        }
    }
}
