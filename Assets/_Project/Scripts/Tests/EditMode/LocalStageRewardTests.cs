using System;
using System.IO;
using System.Linq;
using Game.Core;
using Game.Network;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class LocalStageRewardTests
    {
        private string _path;
        private LocalSaveStore _store;
        private GameDataStore _data;
        private LocalBattleApi _api;

        [SetUp]
        public void SetUp()
        {
            _path = Path.Combine(Path.GetTempPath(), $"stage-rewards-{Guid.NewGuid()}.json");
            _store = new LocalSaveStore(_path);
            _data = new GameDataStore();
            _api = new LocalBattleApi(_store, _data);
            var fixture = StageRewardRulesTests.Balance();
            var balance = _data.StageRewards.GetOrThrow(1);
            balance.BaseExp = fixture.BaseExp; balance.ThreeStarBonusExp = fixture.ThreeStarBonusExp;
            balance.Wave3SkillMaterialBonusAmount = fixture.Wave3SkillMaterialBonusAmount;
            balance.Wave4EquipmentMaterials = fixture.Wave4EquipmentMaterials;
            balance.Wave5GemChestCount = fixture.Wave5GemChestCount; balance.Wave6CoinBonus = fixture.Wave6CoinBonus;
            var save = _store.Load();
            save.wallet.energyStored = 10; save.wallet.energyUpdatedAt = DateTime.UtcNow.ToString("O");
            _store.Flush(save);
        }

        [TearDown] public void TearDown() { if (File.Exists(_path)) { File.Delete(_path); } }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(5)]
        [TestCase(6)]
        public void FailedOrForfeitedBattle_PaysSecuredRewardsOnce(int completed)
        {
            var issued = _api.StartBattle(1, 1).GetAwaiter().GetResult();
            var request = new SubmitResultRequest { battleId = issued.battleId, completedWaves = completed, reachedWave = completed + 1 };
            var result = _api.SubmitResult(request).GetAwaiter().GetResult();
            var expected = StageRewardRules.Calculate(_data.StageRewards.GetOrThrow(1), completed, false, 0);
            Assert.AreEqual(expected.Coin, result.rewardGold);
            Assert.AreEqual(expected.Exp, result.rewardExp);
            Assert.AreEqual(completed >= 5, result.rewardItems.Exists(item => item.itemId == ItemIds.GemChest));
            var snapshot = new LocalPlayerApi(_store).GetMe().GetAwaiter().GetResult();
            Assert.AreEqual(expected.Exp, snapshot.exp);
            Assert.AreEqual(expected.Coin, snapshot.gold);
            var before = File.ReadAllText(_path);
            request.cleared = true; request.completedWaves = 20;
            _data.StageRewards.GetOrThrow(1).Wave6CoinBonus = 999;
            var replay = new LocalBattleApi(new LocalSaveStore(_path), _data).SubmitResult(request).GetAwaiter().GetResult();
            Assert.IsFalse(replay.cleared);
            Assert.AreEqual(result.rewardExp, replay.rewardExp);
            Assert.AreEqual(before, File.ReadAllText(_path));
        }

        [TestCase(100, 3, 150)]
        [TestCase(50, 2, 100)]
        [TestCase(20, 1, 100)]
        public void Wave20Clear_PaysExpByRating_AndDoesNotAlsoPayLegacyClearGold(int hp, int rating, int exp)
        {
            _data.Stages.GetOrThrow(1).ClearGold = 9999;
            var issued = _api.StartBattle(1, 1).GetAwaiter().GetResult();
            var result = _api.SubmitResult(new SubmitResultRequest
            { battleId = issued.battleId, completedWaves = 20, reachedWave = 20, cleared = true, wallHpPercent = hp }).GetAwaiter().GetResult();
            Assert.AreEqual(165, result.rewardGold); Assert.AreEqual(exp, result.rewardExp);
            Assert.AreEqual(rating, result.clearRating);
            Assert.AreEqual(rating, _store.Load().stageProgress[0].clearRating);
        }

        [Test]
        public void RandomReward_UsesLatestProvidedUnlocks_AndPersistsResolvedItemsForReplay()
        {
            _data.StageRewards.GetOrThrow(1).SkillMaterial = ItemIds.RandomSkillMaterial;
            _api.UnlockedSkillIds = () => new[] { 1 };
            var issued = _api.StartBattle(1, 1).GetAwaiter().GetResult();
            _api.UnlockedSkillIds = () => new[] { 2, 3 };
            var request = new SubmitResultRequest { battleId = issued.battleId, completedWaves = 3, reachedWave = 4 };
            var result = _api.SubmitResult(request).GetAwaiter().GetResult();
            Assert.AreEqual(34, result.rewardItems.Sum(item => item.quantity));
            Assert.IsTrue(result.rewardItems.All(item => item.itemId == ItemIds.FireballBook || item.itemId == ItemIds.LightningBook));
            var persisted = File.ReadAllText(_path);
            _api.UnlockedSkillIds = () => new[] { 1 };
            _api.SubmitResult(request).GetAwaiter().GetResult();
            Assert.AreEqual(persisted, File.ReadAllText(_path));
            Assert.IsFalse(_store.Load().items.Exists(item => item.itemId == ItemIds.RandomSkillMaterial));
        }

        [Test]
        public void MissingUnlockIntegration_RejectsBeforeEnergyConsumption_AndFailedResolveDoesNotFinalize()
        {
            _data.StageRewards.GetOrThrow(1).SkillMaterial = ItemIds.RandomSkillMaterial;
            var before = File.ReadAllText(_path);
            var error = Assert.Throws<ApiException>(() => _api.StartBattle(1, 1).GetAwaiter().GetResult());
            Assert.AreEqual("SKILL_UNLOCK_SOURCE_NOT_READY", error.Code); Assert.AreEqual(before, File.ReadAllText(_path));
            _api.UnlockedSkillIds = () => Array.Empty<int>();
            var issued = _api.StartBattle(1, 1).GetAwaiter().GetResult();
            before = File.ReadAllText(_path);
            Assert.Throws<ApiException>(() => _api.SubmitResult(new SubmitResultRequest { battleId = issued.battleId }).GetAwaiter().GetResult());
            Assert.AreEqual(before, File.ReadAllText(_path));
        }

        [TestCase(-1, false)]
        [TestCase(21, false)]
        [TestCase(19, true)]
        public void InvalidCompletedWaves_DoNotFinalizeOrPay(int completed, bool clear)
        {
            var issued = _api.StartBattle(1, 1).GetAwaiter().GetResult();
            var before = File.ReadAllText(_path);
            Assert.Throws<ApiException>(() => _api.SubmitResult(new SubmitResultRequest
            { battleId = issued.battleId, completedWaves = completed, reachedWave = 20, cleared = clear }).GetAwaiter().GetResult());
            Assert.AreEqual(before, File.ReadAllText(_path));
        }
    }
}
