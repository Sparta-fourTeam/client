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
    }
}
