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
            public IWeaponStats CurrentStats => stats;
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
            Assert.AreEqual(10 * (1 + before / 100), lowWeapon.CurrentStats.Damage, 0.0001f);
            Assert.AreEqual(10 * (1 + after / 100), highWeapon.CurrentStats.Damage, 0.0001f);
            Assert.AreEqual(2, lowWeapon.CurrentStats.CastCount);
            Assert.AreEqual(2, highWeapon.CurrentStats.CastCount);
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
            Assert.AreEqual(14, weapon.CurrentStats.ProjectileSpeed, 0.0001f);
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
            Assert.AreEqual(10, weapon.CurrentStats.Damage);
            Assert.AreEqual(0, weapon.UpgradeCount);
        }

        private static TestWeapon NewWeapon() => new TestWeapon(new WeaponData
        {
            maxLevel = 15,
            baseStats = new WeaponBaseStats { baseDamage = 10, speed = 20, hitCount = 1 }
        });

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

        private static IWeaponStats Stats() => new BaseWeaponStats(new WeaponBaseStats
        {
            cooldown = 2f,
            baseDamage = 10f,
            hitCount = 1,
            speed = 20f
        });

        [Test]
        public void CountUpgrades_AreIndependent()
        {
            IWeaponStats stats = new ProjectileCountUpgrade(Stats(), 1);
            stats = new CastCountUpgrade(stats, 2);
            stats = new PierceCountUpgrade(stats, 3);
            Assert.AreEqual(2, stats.ProjectileCount);
            Assert.AreEqual(3, stats.CastCount);
            Assert.AreEqual(3, stats.PierceCount);
            Assert.AreEqual(20f, stats.ProjectileSpeed);
        }

        [Test]
        public void SpeedUpgrade_DoesNotChangeCooldown()
        {
            var stats = new ProjectileSpeedUpgrade(Stats(), 25);
            Assert.AreEqual(25f, stats.ProjectileSpeed);
            Assert.AreEqual(2f, stats.Cooldown);
        }

        [Test]
        public void LegacyHitCount_StillMeansProjectiles()
        {
            var stats = new HitCountUpgrade(Stats(), 2);
            Assert.AreEqual(3, stats.ProjectileCount);
            Assert.AreEqual(3, stats.HitCount);
            Assert.AreEqual(0, stats.PierceCount);
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
