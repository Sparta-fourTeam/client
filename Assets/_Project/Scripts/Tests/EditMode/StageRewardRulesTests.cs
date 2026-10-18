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
            RewardRules = RulesFixture(),
            BaseExp = 100,
            ThreeStarBonusExp = 50,
            FinalCoin = 165,
            Items = new List<StageRewardEntry> { new() { itemId = ItemIds.ArrowBook, finalAmount = 51 } }
                .Concat(ItemIds.EquipmentMaterials.Select(id => new StageRewardEntry { itemId = id, finalAmount = 2 }))
                .Append(new StageRewardEntry { itemId = ItemIds.GemChest, finalAmount = 1 })
                .ToList()
        };

        internal static RewardRulesConfig RulesFixture()
        {
            var rules = new RewardRulesConfig
            {
                Schedules = new Dictionary<string, RewardSchedule>
                {
                    ["Coin"] = new(25, 5, 6),
                    ["SkillMaterial"] = new(1, 1, 3),
                    ["EquipmentMaterial"] = new(0, 0, 4),
                    ["GemChest"] = new(0, 0, 5)
                },
                Items = new Dictionary<string, RewardItemRule>()
            };
            foreach (var id in ItemIds.SkillMaterials) { rules.Items[id] = new RewardItemRule { Kind = "SkillMaterial" }; }
            foreach (var id in ItemIds.EquipmentMaterials) { rules.Items[id] = new RewardItemRule { Kind = "EquipmentMaterial" }; }
            rules.Items[ItemIds.GemChest] = new RewardItemRule { Kind = "GemChest" };
            rules.Items[ItemIds.RandomSkillMaterial] = new RewardItemRule { Kind = "SkillMaterial", Random = true };
            rules.Items[ItemIds.RandomEquipmentMaterial] = new RewardItemRule { Kind = "EquipmentMaterial", Random = true };
            return rules;
        }

        [Test]
        public void ZeroWavesKeepConfiguredStartingRewardsWithoutExperience()
        {
            var balance = Balance();
            balance.RewardRules.Schedules["Coin"] = new RewardSchedule(7, 3, 6);
            balance.RewardRules.Schedules["SkillMaterial"] = new RewardSchedule(3, 1, 3);
            var rewards = StageRewardRules.Calculate(balance, 0, false, 0);
            Assert.AreEqual(7, rewards.Coin);
            Assert.AreEqual(0, rewards.Exp);
            Assert.AreEqual(1, rewards.Items.Count);
            Assert.AreEqual(ItemIds.ArrowBook, rewards.Items.Single().itemId);
            Assert.AreEqual(3, rewards.Items.Single().quantity);
        }

        [Test]
        public void SchedulesAndArbitraryItemClassificationComeFromData()
        {
            var balance = Balance();
            balance.RewardRules.Schedules["Coin"] = new RewardSchedule(9, 2, 3);
            balance.RewardRules.Items["new.material"] = new RewardItemRule { Kind = "EquipmentMaterial" };
            balance.Items = new List<StageRewardEntry> { new() { itemId = "new.material", finalAmount = 7 } };
            var before = StageRewardRules.Calculate(balance, 2, false, 0);
            var after = StageRewardRules.Calculate(balance, 4, false, 0);
            Assert.AreEqual(13, before.Coin);
            Assert.IsEmpty(before.Items);
            Assert.AreEqual(133, after.Coin);
            Assert.AreEqual("new.material", after.Items.Single().itemId);
            Assert.AreEqual(7, after.Items.Single().quantity);
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

        [TestCase(13, ItemIds.EnergyBeamBook)]
        [TestCase(15, ItemIds.ChainLightningBook)]
        [Test(Description = "서버 ID로 해당 스킬만 열려 있으면 랜덤 보상은 그 스킬의 재료를 지급한다. ID 재정렬 후 누락·잘못된 스킬 재료 지급을 막는다")]
        public void RandomSkillMaterial_ServerSkillIdAwardsItsOwnBook(int skillId, string expectedBook)
        {
            var data = new GameDataStore();
            var rewards = new[] { new ItemAmount { itemId = ItemIds.RandomSkillMaterial, quantity = 7 } };
            var resolved = RandomMaterialResolver.Resolve(rewards, new[] { skillId }, null, data, seed: 1);

            Assert.That(resolved.Count, Is.EqualTo(1));
            Assert.That(resolved[0].itemId, Is.EqualTo(expectedBook));
            Assert.That(resolved[0].quantity, Is.EqualTo(7));
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

        [Test(Description = "확률 안내는 지급과 같은 후보를 균등 확률로 보여주고, 열린 재료가 없으면 후보도 없다")]
        public void RandomRewardOdds_ListsTheSameCandidatesTheResolverCanPick_WithEqualProbability()
        {
            var data = new GameDataStore();
            var odds = RandomRewardOdds.For(ItemIds.RandomSkillMaterial, new[] { 1, 2, 3, 999 }, null, data);

            CollectionAssert.AreEquivalent(new[] { ItemIds.ArrowBook, ItemIds.FireballBook, ItemIds.LightningBook }, odds.Select(odd => odd.ItemId));
            Assert.That(odds.Sum(odd => odd.Probability), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(odds.Select(odd => odd.Probability), Is.All.EqualTo(1f / 3f).Within(1e-5f));
            var rewards = new[] { new ItemAmount { itemId = ItemIds.RandomSkillMaterial, quantity = 40 } };
            for (int seed = 1; seed <= 100; seed++)
            {
                var picked = RandomMaterialResolver.Resolve(rewards, new[] { 1, 2, 3, 999 }, null, data, seed).Select(item => item.itemId);
                Assert.That(picked, Is.SubsetOf(odds.Select(odd => odd.ItemId)));
            }

            Assert.AreEqual("33.33%", RandomRewardOdds.Percent(1f / 3f));
            Assert.IsEmpty(RandomRewardOdds.For(ItemIds.RandomEquipmentMaterial, new[] { 1 }, Array.Empty<string>(), data));
            Assert.IsEmpty(RandomRewardOdds.For(ItemIds.ArrowBook, new[] { 1 }, null, data));
        }

        [Test(Description = "확률표는 그 레벨에 열린 스킬·장비의 재료만 보여주고, 레벨이 오르면 후보가 늘어난다")]
        public void RandomRewardOdds_ForExp_ListsOnlyMaterialsUnlockedAtThatLevel()
        {
            var data = new GameDataStore();
            var skills = data.LoadSkills();
            var expectedBooks = ItemIds.SkillMaterials.Where(id =>
                skills.Any(skill => skill.unlockLevel <= 1 && skill.id.ToString() == data.Items.GetOrThrow(id).TargetId)).ToList();
            var expectedScrolls = ItemIds.EquipmentMaterials.Where(id =>
                data.Upgrades.GetOrThrow(data.Items.GetOrThrow(id).TargetId).UnlockLevel <= 1).ToList();

            var books = RandomRewardOdds.ForExp(ItemIds.RandomSkillMaterial, data, 0);
            var scrolls = RandomRewardOdds.ForExp(ItemIds.RandomEquipmentMaterial, data, 0);

            CollectionAssert.AreEquivalent(expectedBooks, books.Select(odd => odd.ItemId));
            CollectionAssert.AreEquivalent(expectedScrolls, scrolls.Select(odd => odd.ItemId));
            Assert.Greater(RandomRewardOdds.ForExp(ItemIds.RandomSkillMaterial, data, int.MaxValue / 4).Count, books.Count);
            Assert.Greater(RandomRewardOdds.ForExp(ItemIds.RandomEquipmentMaterial, data, int.MaxValue / 4).Count, scrolls.Count);
        }

        [Test(Description = "총수량이 기본 누적보다 작으면 중간 보너스가 음수가 되므로 거절한다")]
        public void FinalAmountBelowBaseAccumulation_IsRejected()
        {
            var coin = Balance();
            coin.FinalCoin = coin.RewardRules.Coin.MinimumFinal - 1;
            Assert.Throws<InvalidOperationException>(coin.Validate);
            var skill = Balance();
            skill.Items[0].finalAmount = skill.RewardRules.Schedules["SkillMaterial"].MinimumFinal - 1;
            Assert.Throws<InvalidOperationException>(skill.Validate);
        }
    }
}
