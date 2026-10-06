using System;
using System.Collections.Generic;
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
        public void RandomSkillMaterial_PicksFromUnlockedWithReplacement_AndSplitsEvenly()
        {
            var data = new GameDataStore();
            var rewards = new[] { new ItemAmount { itemId = ItemIds.RandomSkillMaterial, quantity = 51 } };
            var allowed = new[] { ItemIds.ArrowBook, ItemIds.FireballBook, ItemIds.LightningBook };
            var sawSame = false;
            var sawDifferent = false;
            for (int seed = 1; seed <= 200; seed++)
            {
                var resolved = RandomMaterialResolver.Resolve(rewards, new[] { 1, 2, 3, 1, 999 }, null, data, seed);
                Assert.AreEqual(51, resolved.Sum(item => item.quantity));
                Assert.IsTrue(resolved.All(item => allowed.Contains(item.itemId)));
                if (resolved.Count == 1) { sawSame = true; Assert.AreEqual(51, resolved[0].quantity); }
                else { sawDifferent = true; CollectionAssert.AreEquivalent(new[] { 26, 25 }, resolved.Select(item => item.quantity)); }
            }
            Assert.IsTrue(sawSame && sawDifferent, "같은 재료가 두 번 뽑히는 경우와 서로 다른 두 재료가 뽑히는 경우가 모두 나와야 합니다.");
        }

        [Test]
        public void RandomEquipmentMaterial_PicksOnlyUnlockedEquipment()
        {
            var data = new GameDataStore();
            var rewards = new[] { new ItemAmount { itemId = ItemIds.RandomEquipmentMaterial, quantity = 1 } };
            var unlocked = new[] { data.Items.GetOrThrow(ItemIds.WeaponBook).TargetId, data.Items.GetOrThrow(ItemIds.HatBook).TargetId };
            for (int seed = 1; seed <= 50; seed++)
            {
                var resolved = RandomMaterialResolver.Resolve(rewards, null, unlocked, data, seed);
                Assert.AreEqual(1, resolved.Count);
                Assert.AreEqual(1, resolved[0].quantity);
                Assert.IsTrue(resolved[0].itemId == ItemIds.WeaponBook || resolved[0].itemId == ItemIds.HatBook);
            }
            Assert.Throws<ApiException>(() => RandomMaterialResolver.Resolve(rewards, new[] { 1 }, Array.Empty<string>(), data, 1));
        }

        [Test]
        public void FixedMaterial_DoesNotNeedUnlocks_AndRandomDistributionCanBeReplaced()
        {
            var data = new GameDataStore();
            var fixedReward = new ItemAmount { itemId = ItemIds.ArrowBook, quantity = 30 };
            var fixedResult = RandomMaterialResolver.Resolve(new[] { fixedReward }, Array.Empty<int>(), null, data, 1,
                (_, _) => throw new Exception("고정 재료에서는 호출하면 안 됩니다"));
            Assert.AreEqual(30, fixedResult[0].quantity);
            var random = new[] { new ItemAmount { itemId = ItemIds.RandomSkillMaterial, quantity = 50 } };
            int calledQuantity = 0, calledCount = 0;
            var custom = RandomMaterialResolver.Resolve(random, new[] { 1, 2 }, null, data, 1,
                (quantity, count) => { calledQuantity = quantity; calledCount = count; return new[] { 20, 30 }; });
            Assert.AreEqual((50, 2), (calledQuantity, calledCount));
            Assert.AreEqual(50, custom.Sum(item => item.quantity));
            Assert.Throws<ApiException>(() => RandomMaterialResolver.Resolve(random, Array.Empty<int>(), null, data, 1));
            Assert.Throws<ApiException>(() => RandomMaterialResolver.Resolve(random, new[] { 1, 2 }, null, data, 1, (_, _) => new[] { 10, 10 }));
            var single = RandomMaterialResolver.Resolve(random, new[] { 2 }, null, data, 1);
            Assert.AreEqual(ItemIds.FireballBook, single[0].itemId);
            Assert.AreEqual(50, single[0].quantity);
        }

        [Test]
        public void EquipmentMaterialList_AllowsFixedOrRandomEntries_ButNotUnknownOrDuplicate()
        {
            var balance = Balance();
            balance.Wave4EquipmentMaterials = new List<ItemAmount> { new() { itemId = ItemIds.RandomEquipmentMaterial, quantity = 1 } };
            Assert.DoesNotThrow(balance.Validate);
            Assert.IsTrue(balance.UsesRandomEquipmentMaterial);
            balance.Wave4EquipmentMaterials = new List<ItemAmount> { new() { itemId = ItemIds.ArrowBook, quantity = 1 } };
            Assert.Throws<InvalidOperationException>(balance.Validate);
            balance.Wave4EquipmentMaterials = new List<ItemAmount>
            { new() { itemId = ItemIds.WeaponBook, quantity = 1 }, new() { itemId = ItemIds.WeaponBook, quantity = 1 } };
            Assert.Throws<InvalidOperationException>(balance.Validate);
        }
    }
}
