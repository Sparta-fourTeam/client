using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class StageRewardRulesTests
    {
        /// <summary>기본 누적(코인 125, 스킬 재료 21)에 중간 보너스 코인 40, 스킬 재료 30이 붙는 총수량</summary>
        internal static StageRewardBalance Balance() => new()
        {
            StageId = 1,
            BaseExp = 100,
            ThreeStarBonusExp = 50,
            FinalCoin = 165,
            Items = new List<StageRewardEntry> { new() { itemId = ItemIds.ArrowBook, finalAmount = 51 } }
                .Concat(ItemIds.EquipmentMaterials.Select(id => new StageRewardEntry { itemId = id, finalAmount = 2 }))
                .Append(new StageRewardEntry { itemId = ItemIds.GemChest, finalAmount = 1 })
                .ToList()
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
        public void ItemEntries_AllowFixedOrRandomMaterials_ButNotUnknownOrDuplicate()
        {
            var balance = Balance();
            balance.Items = new List<StageRewardEntry> { new() { itemId = ItemIds.RandomEquipmentMaterial, finalAmount = 1 } };
            Assert.DoesNotThrow(balance.Validate);
            Assert.IsTrue(balance.UsesRandomEquipmentMaterial);
            balance.Items = new List<StageRewardEntry> { new() { itemId = "item.unknown", finalAmount = 1 } };
            Assert.Throws<InvalidOperationException>(balance.Validate);
            balance.Items = new List<StageRewardEntry>
            { new() { itemId = ItemIds.WeaponBook, finalAmount = 1 }, new() { itemId = ItemIds.WeaponBook, finalAmount = 1 } };
            Assert.Throws<InvalidOperationException>(balance.Validate);
        }

        [Test(Description = "총수량이 기본 누적보다 작으면 중간 보너스가 음수가 되므로 거절한다")]
        public void FinalAmountBelowBaseAccumulation_IsRejected()
        {
            var coin = Balance();
            coin.FinalCoin = StageRewardSchedule.Coin.MinimumFinal - 1;
            Assert.Throws<InvalidOperationException>(coin.Validate);
            var skill = Balance();
            skill.Items[0].finalAmount = StageRewardSchedule.SkillMaterial.MinimumFinal - 1;
            Assert.Throws<InvalidOperationException>(skill.Validate);
        }

        [Test(Description = "중간 보너스는 총수량에서 계산되므로 총수량만 바꿔도 맞춰진다")]
        public void Bonus_IsDerivedFromFinalAmount()
        {
            var balance = Balance();
            balance.FinalCoin = 200;
            Assert.AreEqual(25 + 5 * 5, StageRewardRules.Calculate(balance, 5, false, 0).Coin);
            Assert.AreEqual(25 + 5 * 6 + (200 - 125), StageRewardRules.Calculate(balance, 6, false, 0).Coin);
            Assert.AreEqual(200, StageRewardRules.Maximum(balance).Coin);
        }

        [Test(Description = "새 아이템 보상은 목록에 항목만 추가하면 규칙대로 지급된다")]
        public void NewItemEntry_FollowsItsKindSchedule()
        {
            var balance = Balance();
            balance.Items.Add(new StageRewardEntry { itemId = ItemIds.FireballBook, finalAmount = 21 });
            Assert.DoesNotThrow(balance.Validate);
            Assert.AreEqual(1 + 2, StageRewardRules.Calculate(balance, 2, false, 0).Items.Single(i => i.itemId == ItemIds.FireballBook).quantity);
            Assert.AreEqual(21, StageRewardRules.Maximum(balance).Items.Single(i => i.itemId == ItemIds.FireballBook).quantity);
        }
    }
}
