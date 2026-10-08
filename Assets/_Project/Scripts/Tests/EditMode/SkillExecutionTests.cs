using System;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class SkillExecutionTests
    {
        private sealed class TestSkill : SkillBase
        {
            public TestSkill(SkillData data) : base(data, null, null) { }
            protected override void OnFire() { }
            public SkillStats CurrentStats => stats;
        }

        [TestCase(5, -20, 0)]
        [TestCase(9, -20, 0)]
        [TestCase(17, -30, 10)]
        public void PermanentVariant_ChangesPresentationAndDamageAtBoundary(int gate, float before, float after)
        {
            var option = VariantOption(gate, before, after);
            Assert.IsTrue(SkillUpgradeResolver.TryResolve(option, gate - 1, out var low));
            Assert.AreEqual("연발", low.Name);
            Assert.IsTrue(SkillUpgradeResolver.TryResolve(option, gate, out var high));
            Assert.AreEqual("연발(+)", high.Name);
            var lowWeapon = NewWeapon();
            var highWeapon = NewWeapon();
            Assert.IsTrue(lowWeapon.LevelUp(option, gate - 1));
            Assert.IsTrue(highWeapon.LevelUp(option, gate));
            Assert.AreEqual(10 * (1 + before / 100), lowWeapon.CurrentStats.Cast.Damage, 0.0001f);
            Assert.AreEqual(10 * (1 + after / 100), highWeapon.CurrentStats.Cast.Damage, 0.0001f);
            Assert.AreEqual(2, lowWeapon.CurrentStats.Cast.Count);
            Assert.AreEqual(2, highWeapon.CurrentStats.Cast.Count);
            Assert.AreEqual(before, option.effects[1].value, "변형 해석은 원본 효과를 수정하지 않는다");
            Assert.AreEqual(1, highWeapon.GetAcquiredCount(option.id));
        }

        [Test]
        public void Variant_DoesNotResetChoiceCounterOrRemoveOtherPenalties()
        {
            var option = VariantOption(9, -20, 0);
            option.maxPickCount = 1;
            option.variants[0].effects.Add(new EffectDef { kind = "projectileSpeed", value = -30 });
            var weapon = NewWeapon();
            Assert.IsTrue(weapon.LevelUp(option, 9));
            Assert.AreEqual(14, weapon.CurrentStats.Projectile.Speed, 0.0001f);
            Assert.IsFalse(weapon.LevelUp(option, 8));
            Assert.AreEqual(1, weapon.UpgradeCount);
        }

        [Test]
        public void InvalidVariant_RejectsWithoutApplyingBaseEffects()
        {
            var option = VariantOption(9, -20, 0);
            option.variants[0].effects = null;
            var weapon = NewWeapon();
            Assert.IsFalse(weapon.LevelUp(option, 9));
            Assert.AreEqual(10, weapon.CurrentStats.Cast.Damage);
            Assert.AreEqual(0, weapon.UpgradeCount);
        }

        private static TestSkill NewWeapon() => new TestSkill(new SkillData
        {
            maxLevel = 15,
            baseStats = new SkillBaseStats { cast = { baseDamage = 10, projectileCount = 1 }, projectile = { speed = 20 } }
        });

        private static TestSkill SharedWeapon(int id, string type = "damage")
        {
            var option = new SkillUpgradeOption
            {
                id = "shared_" + id,
                sharedId = "pair",
                affectedWeaponIds = new[] { 1, 2 },
                maxPickCount = 1,
                effects = new System.Collections.Generic.List<EffectDef>
                {
                    new EffectDef { kind = type, value = type == "damage" ? 80 : 20 }
                }
            };
            return new TestSkill(new SkillData
            {
                id = id,
                assetKey = "test_" + id,
                maxLevel = 15,
                baseStats = new SkillBaseStats { cast = { baseDamage = 10, cooldown = 2, projectileCount = 1 } },
                upgrades = new System.Collections.Generic.List<SkillUpgradeOption> { option }
            });
        }

        [TestCase(1, 16, "arrow_neutral_1", "energy_beam_neutral_1", 1.5f)]
        [TestCase(3, 18, "lightning_neutral", "chain_lightning_neutral", 1.8f)]
        public void ShippedSharedCards_UpdateBothSkillsAndCannotBeSelectedTwice(
            int firstId, int secondId, string firstCardId, string secondCardId, float multiplier)
        {
            var catalog = new DefaultSkillDataProvider(new GameDataStore()).LoadAll();
            var first = new TestSkill(catalog.Find(s => s.id == firstId));
            var second = new TestSkill(catalog.Find(s => s.id == secondId));
            var firstCard = first.Data.upgrades.Find(c => c.id == firstCardId);
            var secondCard = second.Data.upgrades.Find(c => c.id == secondCardId);
            var skills = new SkillBase[] { first, second };
            var firstDamage = first.CurrentStats.Cast.Damage;
            var secondDamage = second.CurrentStats.Cast.Damage;
            Assert.IsTrue(firstCard.enabled);
            Assert.IsTrue(secondCard.enabled);
            Assert.IsFalse(SkillUpgradeTransaction.TryApply(first, firstCard, new[] { first }, 0));
            Assert.AreEqual(0, first.UpgradeCount);
            Assert.IsTrue(SkillUpgradeTransaction.TryApply(first, firstCard, skills, 0));
            Assert.AreEqual(firstDamage * multiplier, first.CurrentStats.Cast.Damage, .0001f);
            Assert.AreEqual(secondDamage * multiplier, second.CurrentStats.Cast.Damage, .0001f);
            Assert.AreEqual(1, first.UpgradeCount);
            Assert.AreEqual(1, second.UpgradeCount);
            Assert.AreEqual(1, first.GetAcquiredCount(firstCardId));
            Assert.AreEqual(1, second.GetAcquiredCount(secondCardId));
            Assert.IsFalse(SkillUpgradeTransaction.TryApply(second, secondCard, skills, 0));
        }

        [TestCase("damage")]
        [TestCase("attackSpeed")]
        public void SharedUpgrade_AppliesBothAndSharesCounterAcrossOppositeChoice(string type)
        {
            var a = SharedWeapon(1, type);
            var b = SharedWeapon(2, type);
            var weapons = new SkillBase[] { a, b };
            Assert.IsTrue(SkillUpgradeTransaction.CanApply(a, a.Data.upgrades[0], weapons, 0));
            Assert.AreEqual(0, a.UpgradeCount, "후보 판정은 상태를 변경하지 않는다");
            Assert.IsTrue(SkillUpgradeTransaction.TryApply(a, a.Data.upgrades[0], weapons, 0));
            Assert.AreEqual(1, a.UpgradeCount);
            Assert.AreEqual(1, b.UpgradeCount);
            Assert.AreEqual(1, a.GetAcquiredCount("shared_1"));
            Assert.AreEqual(1, b.GetAcquiredCount("shared_2"));
            Assert.IsFalse(SkillUpgradeTransaction.TryApply(b, b.Data.upgrades[0], weapons, 0));
            if (type == "damage")
            {
                Assert.AreEqual(18, a.CurrentStats.Cast.Damage, 0.0001f);
                Assert.AreEqual(18, b.CurrentStats.Cast.Damage, 0.0001f);
            }
            else
            {
                Assert.AreEqual(1.6f, a.CurrentStats.Cast.Cooldown, 0.0001f);
                Assert.AreEqual(1.6f, b.CurrentStats.Cast.Cooldown, 0.0001f);
            }
        }

        [Test]
        public void OrdinaryTransaction_UsesPermanentVariantWithoutSharing()
        {
            var weapon = NewWeapon();
            var option = VariantOption(9, -20, 0);
            weapon.Data.upgrades = new System.Collections.Generic.List<SkillUpgradeOption> { option };
            Assert.IsTrue(SkillUpgradeTransaction.TryApply(weapon, option, new SkillBase[] { weapon }, 9));
            Assert.AreEqual(10, weapon.CurrentStats.Cast.Damage);
            Assert.AreEqual(2, weapon.CurrentStats.Cast.Count);
            Assert.AreEqual(1, weapon.GetAcquiredCount(option.id));
        }

        [Test]
        public void SharedUpgrade_MissingOrCappedPartnerLeavesOwnerUnchanged()
        {
            var a = SharedWeapon(1);
            var b = SharedWeapon(2);
            Assert.IsFalse(SkillUpgradeTransaction.TryApply(a, a.Data.upgrades[0], new SkillBase[] { a }, 0));
            b.Data.maxLevel = 1;
            Assert.IsTrue(b.LevelUp(new SkillUpgradeOption
            {
                id = "basic",
                effects = new System.Collections.Generic.List<EffectDef>()
            }));
            Assert.IsFalse(SkillUpgradeTransaction.TryApply(a, a.Data.upgrades[0], new SkillBase[] { a, b }, 0));
            Assert.AreEqual(0, a.UpgradeCount);
            Assert.AreEqual(0, a.GetAcquiredCount("shared_1"));
            Assert.AreEqual(10, a.CurrentStats.Cast.Damage);
            Assert.AreEqual(1, b.UpgradeCount);
        }

        [Test]
        public void SharedUpgrade_InconsistentEffectsAndDirectApplicationAreRejected()
        {
            var a = SharedWeapon(1);
            var b = SharedWeapon(2);
            Assert.IsFalse(a.LevelUp(a.Data.upgrades[0]));
            b.Data.upgrades[0].effects[0].value = 60;
            Assert.IsFalse(SkillUpgradeTransaction.TryApply(a, a.Data.upgrades[0], new SkillBase[] { a, b }, 0));
            Assert.AreEqual(0, a.UpgradeCount);
            Assert.AreEqual(0, b.UpgradeCount);
            Assert.AreEqual(10, a.CurrentStats.Cast.Damage);
        }

        [Test]
        public void Catalog_SharedDefinitionRequiresMatchingParticipantsAndCounters()
        {
            var a = SharedWeapon(1);
            var b = SharedWeapon(2);
            var definitions = new System.Collections.Generic.List<SkillData> { a.Data, b.Data };
            Assert.AreEqual(2, GameDataStore.ParseSkills(Newtonsoft.Json.JsonConvert.SerializeObject(definitions)).Count);
            b.Data.upgrades[0].maxPickCount = 2;
            Assert.Throws<InvalidOperationException>(() => GameDataStore.ParseSkills(Newtonsoft.Json.JsonConvert.SerializeObject(definitions)));
        }

        private static SkillUpgradeOption VariantOption(int gate, float before, float after) => new SkillUpgradeOption
        {
            id = "repeat",
            name = "연발",
            maxPickCount = 2,
            effects = new System.Collections.Generic.List<EffectDef>
            {
                new EffectDef { kind = "castCount", value = 1 },
                new EffectDef { kind = "damage", value = before }
            },
            variants = new[] { new SkillUpgradeVariant
            {
                minPermanentLevel = gate, name = "연발(+)",
                effects = new System.Collections.Generic.List<EffectDef>
                {
                    new EffectDef { kind = "castCount", value = 1 },
                    new EffectDef { kind = "damage", value = after }
                }
            } }
        };

        [TestCase(1)]
        [TestCase(14)]
        [TestCase(15)]
        public void BattleUpgradeCap_ExcludesInitialAcquisition(int cap)
        {
            var weapon = new TestSkill(new SkillData { maxLevel = cap, baseStats = new SkillBaseStats() });
            var option = new SkillUpgradeOption
            {
                id = "repeat",
                maxPickCount = cap + 1,
                effects = new System.Collections.Generic.List<EffectDef>()
            };
            Assert.AreEqual(1, weapon.Level);
            Assert.AreEqual(0, weapon.UpgradeCount);
            Assert.IsFalse(weapon.IsMaxLevel);
            for (int i = 0; i < cap; i++)
            {
                Assert.IsTrue(weapon.LevelUp(option));
            }
            Assert.AreEqual(cap, weapon.UpgradeCount);
            Assert.AreEqual(cap + 1, weapon.Level);
            Assert.IsTrue(weapon.IsMaxLevel);
            Assert.IsFalse(weapon.LevelUp(option));
            Assert.AreEqual(cap, weapon.GetAcquiredCount(option.id));
        }

        private static SkillStats Stats() => SkillStats.FromDefinition(new SkillBaseStats
        {
            cast = { cooldown = 2f, baseDamage = 10f, projectileCount = 1 },
            projectile = { speed = 20f }
        });

        [Test]
        public void CountUpgrades_AreIndependent()
        {
            SkillStats stats = SkillTestFactory.Apply(Stats(), "projectileCount", 1);
            stats = SkillTestFactory.Apply(stats, "castCount", 2);
            stats = SkillTestFactory.Apply(stats, "pierceCount", 3);
            Assert.AreEqual(2, stats.Cast.ProjectileCount);
            Assert.AreEqual(3, stats.Cast.Count);
            Assert.AreEqual(3, stats.Projectile.PierceCount);
            Assert.AreEqual(20f, stats.Projectile.Speed);
        }

        [Test]
        public void SpeedUpgrade_DoesNotChangeCooldown()
        {
            var stats = SkillTestFactory.Apply(Stats(), "projectileSpeed", 25);
            Assert.AreEqual(25f, stats.Projectile.Speed);
            Assert.AreEqual(2f, stats.Cast.Cooldown);
        }

        [Test]
        public void ProjectileCount_AddsProjectilesNotPierce()
        {
            var stats = SkillTestFactory.Apply(Stats(), "projectileCount", 2);
            Assert.AreEqual(3, stats.Cast.ProjectileCount);
            Assert.AreEqual(3, stats.Cast.ProjectileCount);
            Assert.AreEqual(0, stats.Projectile.PierceCount);
        }

        [Test]
        public void RepeatedCasts_FireOverTimeAndCooldownStartsImmediately()
        {
            var clock = new CastClock(); int fired = 0;
            clock.Tick(0, 2, 3, 0.1f, () => fired++);
            Assert.AreEqual(1, fired);
            Assert.AreEqual(2f, clock.RemainingCooldown);
            clock.Tick(0.05f, 2, 3, 0.1f, () => fired++);
            Assert.AreEqual(1, fired);
            clock.Tick(0.2f, 2, 3, 0.1f, () => fired++);
            Assert.AreEqual(3, fired);
            Assert.AreEqual(1.75f, clock.RemainingCooldown, 0.0001f);
        }

        [Test]
        public void PendingRepeat_DoesNotAdvanceWhilePaused()
        {
            var clock = new CastClock(); int fired = 0;
            clock.Tick(0, 2, 2, 0.1f, () => fired++);
            clock.Tick(0, 2, 2, 0.1f, () => fired++);
            Assert.AreEqual(1, fired);
        }

        [Test]
        public void Pierce_DamagesDistinctTargetsOnceAndResetsOnReuse()
        {
            var ledger = new ProjectileHitLedger(); var a = new object(); var b = new object(); var c = new object();
            ledger.Reset(1);
            Assert.IsTrue(ledger.TryHit(a));
            Assert.IsFalse(ledger.TryHit(a));
            Assert.IsFalse(ledger.Exhausted);
            Assert.IsTrue(ledger.TryHit(b));
            Assert.IsTrue(ledger.Exhausted);
            Assert.IsFalse(ledger.TryHit(c));
            ledger.Reset(0);
            Assert.IsTrue(ledger.TryHit(a));
            Assert.IsTrue(ledger.Exhausted);
        }

        [Test]
        public void Catalog_RejectsDanglingImpossibleAndCyclicPrerequisites()
        {
            var a = new SkillUpgradeOption { id = "a", maxPickCount = 1, effects = new System.Collections.Generic.List<EffectDef>() };
            var b = new SkillUpgradeOption { id = "b", maxPickCount = 1, effects = new System.Collections.Generic.List<EffectDef>() };
            var data = new[] { new SkillData
            {
                id = 1, assetKey = "a", maxLevel = 15, baseStats = new SkillBaseStats(),
                upgrades = new System.Collections.Generic.List<SkillUpgradeOption> { a, b }
            } };
            a.requiredCardCounts = new[] { new CardCountRequirement { cardId = "missing" } };
            Assert.Throws<InvalidOperationException>(() => GameDataStore.ParseSkills(Newtonsoft.Json.JsonConvert.SerializeObject(data)));
            a.requiredCardCounts = new[] { new CardCountRequirement { cardId = "b", count = 2 } };
            Assert.Throws<InvalidOperationException>(() => GameDataStore.ParseSkills(Newtonsoft.Json.JsonConvert.SerializeObject(data)));
            a.requiredCardCounts = new[] { new CardCountRequirement { cardId = "b" } };
            b.requiredCardCounts = new[] { new CardCountRequirement { cardId = "a" } };
            Assert.Throws<InvalidOperationException>(() => GameDataStore.ParseSkills(Newtonsoft.Json.JsonConvert.SerializeObject(data)));
        }

        [Test]
        public void Catalog_RejectsDuplicateWeaponIds()
        {
            const string json = "[{\"id\":1,\"maxLevel\":1,\"baseStats\":{},\"upgrades\":[]},{\"id\":1,\"maxLevel\":1,\"baseStats\":{},\"upgrades\":[]}]";
            Assert.Throws<InvalidOperationException>(() => GameDataStore.ParseSkills(json));
        }

        [Test]
        public void Catalog_RejectsDuplicateCardIdsAcrossWeapons()
        {
            const string json = "[{\"id\":1,\"assetKey\":\"a\",\"maxLevel\":1,\"baseStats\":{},\"upgrades\":[{\"id\":\"same\",\"effects\":[]}]},{\"id\":2,\"assetKey\":\"b\",\"maxLevel\":1,\"baseStats\":{},\"upgrades\":[{\"id\":\"same\",\"effects\":[]}]}]";
            Assert.Throws<InvalidOperationException>(() => GameDataStore.ParseSkills(json));
        }

        [Test(Description = "assetKey는 프리팹과 아이콘을 찾는 키라 비어 있거나 다른 스킬과 겹치면 거부한다")]
        public void Catalog_RejectsMissingOrDuplicateAssetKey()
        {
            const string missing = "[{\"id\":1,\"maxLevel\":1,\"baseStats\":{},\"upgrades\":[]}]";
            const string duplicate = "[{\"id\":1,\"assetKey\":\"same\",\"maxLevel\":1,\"baseStats\":{},\"upgrades\":[]},{\"id\":2,\"assetKey\":\"same\",\"maxLevel\":1,\"baseStats\":{},\"upgrades\":[]}]";
            const string valid = "[{\"id\":1,\"assetKey\":\"a\",\"maxLevel\":1,\"baseStats\":{},\"upgrades\":[]},{\"id\":2,\"assetKey\":\"b\",\"maxLevel\":1,\"baseStats\":{},\"upgrades\":[]}]";

            Assert.AreEqual(2, GameDataStore.ParseSkills(valid).Count);
            StringAssert.Contains("assetKey", Assert.Throws<InvalidOperationException>(() => GameDataStore.ParseSkills(missing)).Message);
            StringAssert.Contains("assetKey", Assert.Throws<InvalidOperationException>(() => GameDataStore.ParseSkills(duplicate)).Message);
        }
    }
}
