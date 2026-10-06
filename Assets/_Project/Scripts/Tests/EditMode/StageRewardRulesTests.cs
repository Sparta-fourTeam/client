using System;
using System.Linq;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class StageRewardRulesTests
    {
        internal static StageRewardBalance Balance() => new()
        {
            StageId = 1,
            SkillMaterial = ItemIds.ArrowBook,
            BaseExp = 100,
            ThreeStarBonusExp = 50,
            Wave3SkillMaterialBonusAmount = 30,
            Wave5GemChestCount = 1,
            Wave6CoinBonus = 40,
            Wave4EquipmentMaterials = ItemIds.EquipmentMaterials.Select(id => new ItemAmount { itemId = id, quantity = 2 }).ToList()
        };

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        [TestCase(19)]
        [TestCase(20)]
        public void Milestones_AddToBaseAccumulation_AndRemainOnFailure(int completed)
        {
            var result = StageRewardRules.Calculate(Balance(), completed, false, 0);
            Assert.AreEqual(25 + 5 * completed + (completed >= 6 ? 40 : 0), result.Coin);
            Assert.AreEqual(completed >= 1 ? 100 : 0, result.Exp);
            Assert.AreEqual(1 + completed + (completed >= 3 ? 30 : 0), result.Items.Single(item => item.itemId == ItemIds.ArrowBook).quantity);
            foreach (var id in ItemIds.EquipmentMaterials)
            { Assert.AreEqual(completed >= 4 ? 2 : 0, result.Items.Find(item => item.itemId == id)?.quantity ?? 0); }
            Assert.AreEqual(completed >= 5 ? 1 : 0, result.Items.Find(item => item.itemId == ItemIds.GemChest)?.quantity ?? 0);
        }

        [Test]
        public void ThreeStar_AddsOnlyBonusExp_AndMaximumUsesSameCalculator()
        {
            var balance = Balance();
            Assert.AreEqual(100, StageRewardRules.Calculate(balance, 20, true, 2).Exp);
            Assert.AreEqual(150, StageRewardRules.Maximum(balance).Exp);
            var summary = new StageRewardSummary(165, 150, StageRewardRules.Maximum(balance).Items);
            Assert.AreEqual(51, summary.SkillMaterial);
            Assert.AreEqual(14, summary.EquipmentMaterial);
            Assert.AreEqual(1, summary.GemChest);
        }

        [TestCase(-1, false, 0)]
        [TestCase(21, false, 0)]
        [TestCase(19, true, 3)]
        [TestCase(20, false, 3)]
        public void InvalidProgress_IsRejected(int completed, bool clear, int rating)
        { Assert.Throws<ArgumentException>(() => StageRewardRules.Calculate(Balance(), completed, clear, rating)); }

        [Test]
        public void RandomMaterial_SelectsOnlyUnlocked_MaximumTwoTypes_AndConservesQuantity()
        {
            var data = new GameDataStore();
            var rewards = new[] { new ItemAmount { itemId = ItemIds.RandomSkillMaterial, quantity = 50 } };
            for (int seed = 1; seed <= 100; seed++)
            {
                var resolved = SkillMaterialResolver.Resolve(rewards, new[] { 1, 2, 3, 1, 999 }, data, seed);
                Assert.AreEqual(2, resolved.Count);
                Assert.AreEqual(50, resolved.Sum(item => item.quantity));
                Assert.AreEqual(2, resolved.Select(item => item.itemId).Distinct().Count());
                Assert.IsTrue(resolved.All(item => new[] { ItemIds.ArrowBook, ItemIds.FireballBook, ItemIds.LightningBook }.Contains(item.itemId)));
            }
        }

        [Test]
        public void FixedMaterial_DoesNotNeedUnlocks_AndRandomDistributionCanBeReplaced()
        {
            var data = new GameDataStore();
            var fixedReward = new ItemAmount { itemId = ItemIds.ArrowBook, quantity = 30 };
            var fixedResult = SkillMaterialResolver.Resolve(new[] { fixedReward }, Array.Empty<int>(), data, 1,
                (_, _) => throw new Exception("고정 재료에서는 호출하면 안 됩니다"));
            Assert.AreEqual(30, fixedResult[0].quantity);
            var random = new[] { new ItemAmount { itemId = ItemIds.RandomSkillMaterial, quantity = 50 } };
            var custom = SkillMaterialResolver.Resolve(random, new[] { 1, 2 }, data, 1, (_, _) => new[] { 20, 30 });
            CollectionAssert.AreEqual(new[] { 20, 30 }, custom.Select(item => item.quantity));
            Assert.Throws<ApiException>(() => SkillMaterialResolver.Resolve(random, Array.Empty<int>(), data, 1));
            Assert.Throws<ApiException>(() => SkillMaterialResolver.Resolve(random, new[] { 1, 2 }, data, 1, (_, _) => new[] { 10, 10 }));
            var single = SkillMaterialResolver.Resolve(random, new[] { 2 }, data, 1);
            Assert.AreEqual(ItemIds.FireballBook, single[0].itemId);
            Assert.AreEqual(50, single[0].quantity);
        }
    }
}
