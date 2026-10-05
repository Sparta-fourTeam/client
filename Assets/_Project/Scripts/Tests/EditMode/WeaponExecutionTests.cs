using System;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class WeaponExecutionTests
    {
        private sealed class TestWeapon : WeaponBase
        {
            public TestWeapon(WeaponData data) : base(data, null, null) { }
            protected override void OnFire() { }
            public WeaponStats CurrentStats => stats;
        }

        [TestCase(5, -20, 0)]
        [TestCase(9, -20, 0)]
        [TestCase(17, -30, 10)]
        public void PermanentVariant_ChangesPresentationAndDamageAtBoundary(int gate, float before, float after)
        {
            var option = VariantOption(gate, before, after);
            Assert.IsTrue(WeaponUpgradeResolver.TryResolve(option, gate - 1, out var low));
            Assert.AreEqual("연발", low.Name);
            Assert.IsTrue(WeaponUpgradeResolver.TryResolve(option, gate, out var high));
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
            option.variants[0].effects.Add(new StatEffect { type = UpgradeType.ProjectileSpeed, value = -30 });
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

        private static TestWeapon NewWeapon() => new TestWeapon(new WeaponData
        {
            maxLevel = 15,
            baseStats = new WeaponBaseStats { cast = { baseDamage = 10, projectileCount = 1 }, projectile = { speed = 20 } }
        });

        private static TestWeapon SharedWeapon(int id, UpgradeType type = UpgradeType.Damage)
        {
            var option = new WeaponUpgradeOption
            {
                id = "shared_" + id,
                sharedId = "pair",
                affectedWeaponIds = new[] { 1, 2 },
                maxPickCount = 1,
                effects = new System.Collections.Generic.List<StatEffect>
                {
                    new StatEffect { type = type, value = type == UpgradeType.Damage ? 80 : 20 }
                }
            };
            return new TestWeapon(new WeaponData
            {
                id = id,
                maxLevel = 15,
                baseStats = new WeaponBaseStats { cast = { baseDamage = 10, cooldown = 2, projectileCount = 1 } },
                upgrades = new System.Collections.Generic.List<WeaponUpgradeOption> { option }
            });
        }

        [TestCase(UpgradeType.Damage)]
        [TestCase(UpgradeType.AttackSpeed)]
        public void SharedUpgrade_AppliesBothAndSharesCounterAcrossOppositeChoice(UpgradeType type)
        {
            var a = SharedWeapon(1, type);
            var b = SharedWeapon(2, type);
            var weapons = new WeaponBase[] { a, b };
            Assert.IsTrue(WeaponUpgradeTransaction.CanApply(a, a.Data.upgrades[0], weapons, 0));
            Assert.AreEqual(0, a.UpgradeCount, "후보 판정은 상태를 변경하지 않는다");
            Assert.IsTrue(WeaponUpgradeTransaction.TryApply(a, a.Data.upgrades[0], weapons, 0));
            Assert.AreEqual(1, a.UpgradeCount);
            Assert.AreEqual(1, b.UpgradeCount);
            Assert.AreEqual(1, a.GetAcquiredCount("shared_1"));
            Assert.AreEqual(1, b.GetAcquiredCount("shared_2"));
            Assert.IsFalse(WeaponUpgradeTransaction.TryApply(b, b.Data.upgrades[0], weapons, 0));
            if (type == UpgradeType.Damage)
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
            weapon.Data.upgrades = new System.Collections.Generic.List<WeaponUpgradeOption> { option };
            Assert.IsTrue(WeaponUpgradeTransaction.TryApply(weapon, option, new WeaponBase[] { weapon }, 9));
            Assert.AreEqual(10, weapon.CurrentStats.Cast.Damage);
            Assert.AreEqual(2, weapon.CurrentStats.Cast.Count);
            Assert.AreEqual(1, weapon.GetAcquiredCount(option.id));
        }

        [Test]
        public void SharedUpgrade_MissingOrCappedPartnerLeavesOwnerUnchanged()
        {
            var a = SharedWeapon(1);
            var b = SharedWeapon(2);
            Assert.IsFalse(WeaponUpgradeTransaction.TryApply(a, a.Data.upgrades[0], new WeaponBase[] { a }, 0));
            b.Data.maxLevel = 1;
            Assert.IsTrue(b.LevelUp(new WeaponUpgradeOption
            {
                id = "basic",
                effects = new System.Collections.Generic.List<StatEffect>()
            }));
            Assert.IsFalse(WeaponUpgradeTransaction.TryApply(a, a.Data.upgrades[0], new WeaponBase[] { a, b }, 0));
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
            Assert.IsFalse(WeaponUpgradeTransaction.TryApply(a, a.Data.upgrades[0], new WeaponBase[] { a, b }, 0));
            Assert.AreEqual(0, a.UpgradeCount);
            Assert.AreEqual(0, b.UpgradeCount);
            Assert.AreEqual(10, a.CurrentStats.Cast.Damage);
        }

        [Test]
        public void Catalog_SharedDefinitionRequiresMatchingParticipantsAndCounters()
        {
            var a = SharedWeapon(1);
            var b = SharedWeapon(2);
            var definitions = new System.Collections.Generic.List<WeaponData> { a.Data, b.Data };
            Assert.AreEqual(2, DefaultWeaponDataProvider.Parse(Newtonsoft.Json.JsonConvert.SerializeObject(definitions)).Count);
            b.Data.upgrades[0].maxPickCount = 2;
            Assert.Throws<InvalidOperationException>(() => DefaultWeaponDataProvider.Parse(Newtonsoft.Json.JsonConvert.SerializeObject(definitions)));
        }

        private static WeaponUpgradeOption VariantOption(int gate, float before, float after) => new WeaponUpgradeOption
        {
            id = "repeat",
            name = "연발",
            maxPickCount = 2,
            effects = new System.Collections.Generic.List<StatEffect>
            {
                new StatEffect { type = UpgradeType.CastCount, value = 1 },
                new StatEffect { type = UpgradeType.Damage, value = before }
            },
            variants = new[] { new WeaponUpgradeVariant
            {
                minPermanentLevel = gate, name = "연발(+)",
                effects = new System.Collections.Generic.List<StatEffect>
                {
                    new StatEffect { type = UpgradeType.CastCount, value = 1 },
                    new StatEffect { type = UpgradeType.Damage, value = after }
                }
            } }
        };

        [TestCase(1)]
        [TestCase(14)]
        [TestCase(15)]
        public void BattleUpgradeCap_ExcludesInitialAcquisition(int cap)
        {
            var weapon = new TestWeapon(new WeaponData { maxLevel = cap, baseStats = new WeaponBaseStats() });
            var option = new WeaponUpgradeOption
            {
                id = "repeat",
                maxPickCount = cap + 1,
                effects = new System.Collections.Generic.List<StatEffect>()
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

        private static WeaponStats Stats() => WeaponStats.FromDefinition(new WeaponBaseStats
        {
            cast = { cooldown = 2f, baseDamage = 10f, projectileCount = 1 },
            projectile = { speed = 20f }
        });

        [Test]
        public void CountUpgrades_AreIndependent()
        {
            WeaponStats stats = WeaponStatsTestFactory.Apply(Stats(), UpgradeType.ProjectileCount, 1);
            stats = WeaponStatsTestFactory.Apply(stats, UpgradeType.CastCount, 2);
            stats = WeaponStatsTestFactory.Apply(stats, UpgradeType.PierceCount, 3);
            Assert.AreEqual(2, stats.Cast.ProjectileCount);
            Assert.AreEqual(3, stats.Cast.Count);
            Assert.AreEqual(3, stats.Projectile.PierceCount);
            Assert.AreEqual(20f, stats.Projectile.Speed);
        }

        [Test]
        public void SpeedUpgrade_DoesNotChangeCooldown()
        {
            var stats = WeaponStatsTestFactory.Apply(Stats(), UpgradeType.ProjectileSpeed, 25);
            Assert.AreEqual(25f, stats.Projectile.Speed);
            Assert.AreEqual(2f, stats.Cast.Cooldown);
        }

        [Test]
        public void ProjectileCount_AddsProjectilesNotPierce()
        {
            var stats = WeaponStatsTestFactory.Apply(Stats(), UpgradeType.ProjectileCount, 2);
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
            var a = new WeaponUpgradeOption { id = "a", maxPickCount = 1, effects = new System.Collections.Generic.List<StatEffect>() };
            var b = new WeaponUpgradeOption { id = "b", maxPickCount = 1, effects = new System.Collections.Generic.List<StatEffect>() };
            var data = new[] { new WeaponData
            {
                id = 1, maxLevel = 15, baseStats = new WeaponBaseStats(),
                upgrades = new System.Collections.Generic.List<WeaponUpgradeOption> { a, b }
            } };
            a.requiredCardIds = new[] { "missing" };
            Assert.Throws<InvalidOperationException>(() => DefaultWeaponDataProvider.Parse(Newtonsoft.Json.JsonConvert.SerializeObject(data)));
            a.requiredCardIds = null;
            a.requiredCardCounts = new[] { new CardCountRequirement { cardId = "b", count = 2 } };
            Assert.Throws<InvalidOperationException>(() => DefaultWeaponDataProvider.Parse(Newtonsoft.Json.JsonConvert.SerializeObject(data)));
            a.requiredCardCounts = null;
            a.requiredCardIds = new[] { "b" };
            b.requiredCardIds = new[] { "a" };
            Assert.Throws<InvalidOperationException>(() => DefaultWeaponDataProvider.Parse(Newtonsoft.Json.JsonConvert.SerializeObject(data)));
        }

        [Test]
        public void Catalog_RejectsDuplicateWeaponIds()
        {
            const string json = "[{\"id\":1,\"maxLevel\":1,\"baseStats\":{},\"upgrades\":[]},{\"id\":1,\"maxLevel\":1,\"baseStats\":{},\"upgrades\":[]}]";
            Assert.Throws<InvalidOperationException>(() => DefaultWeaponDataProvider.Parse(json));
        }

        [Test]
        public void Catalog_RejectsDuplicateCardIdsAcrossWeapons()
        {
            const string json = "[{\"id\":1,\"maxLevel\":1,\"baseStats\":{},\"upgrades\":[{\"id\":\"same\",\"effects\":[]}]},{\"id\":2,\"maxLevel\":1,\"baseStats\":{},\"upgrades\":[{\"id\":\"same\",\"effects\":[]}]}]";
            Assert.Throws<InvalidOperationException>(() => DefaultWeaponDataProvider.Parse(json));
        }
    }
}
