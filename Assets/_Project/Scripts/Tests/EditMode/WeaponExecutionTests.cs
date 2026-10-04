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
        }

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
