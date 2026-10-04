using System.Collections.Generic;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public class AreaDamageTests
    {
        private class Target : IEnemyTarget
        {
            public Vector2 Position { get; set; }
            public int Damage;
            public void TakeDamage(int value) => Damage += value;
        }
        private class Provider : IEnemyTargetProvider
        {
            public readonly List<IEnemyTarget> Targets = new();
            public int GetNearest(Vector2 from, int count, List<IEnemyTarget> results)
            { results.Clear(); results.AddRange(Targets); return results.Count; }
        }
        [Test]
        public void HitscanExplosion_FiresOnceAndClearsOnPoolReuse()
        {
            var go = new GameObject("HitscanExplosionTest");
            try
            {
                var effect = go.AddComponent<HitscanEffect>();
                var pool = new UnityEngine.Pool.ObjectPool<HitscanEffect>(() => effect);
                var hit = typeof(HitscanEffect).GetMethod("Hit", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                int count = 0;
                pool.Get().Init(pool, Vector3.zero, 10, position => count++);
                hit.Invoke(effect, null);
                hit.Invoke(effect, null);
                Assert.AreEqual(1, count);
                pool.Release(effect);
                pool.Get().Init(pool, Vector3.zero, 10);
                hit.Invoke(effect, null);
                Assert.AreEqual(1, count);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void ImpactUpgrade_ChangesOnlyDirectDamageAndTrainingScalesBoth()
        {
            var data = new DefaultWeaponDataProvider().LoadAll().Find(w => w.id == 2);
            IWeaponStats stats = new BaseWeaponStats(data.baseStats);
            var impact = data.upgrades.Find(c => c.id == "fireball_impact_damage");
            Assert.AreEqual(3, impact.maxPickCount);
            stats = new ImpactDamageUpgrade(stats, impact.effects[0].value);
            Assert.AreEqual(21.6f, stats.Damage, 0.001f);
            Assert.AreEqual(12, stats.ExplosionDamage, 0.001f);
            stats = new ExplosionUpgrade(stats, 80, false);
            Assert.AreEqual(21.6f, stats.Damage, 0.001f);
            Assert.AreEqual(21.6f, stats.ExplosionDamage, 0.001f);
            stats = new AttackSpeedUpgrade(new DamageUpgrade(stats, 20), 10);
            Assert.AreEqual(25.92f, stats.Damage, 0.001f);
            Assert.AreEqual(25.92f, stats.ExplosionDamage, 0.001f);
            Assert.AreEqual(1.62f, stats.Cooldown, 0.001f);
        }

        [Test]
        public void HighVoltageLightning_IncreasesDamageAndAddsParalysisDuration()
        {
            var data = new DefaultWeaponDataProvider().LoadAll().Find(w => w.id == 3);
            var go = new GameObject("HighVoltageStatsTest");
            try
            {
                var weapon = new HitscanCaster(data, go, go.transform, new Provider());
                var option = data.upgrades.Find(c => c.id == "lightning_voltage");
                Assert.IsTrue(weapon.LevelUp(option));
                Assert.IsFalse(weapon.LevelUp(option));
                var stats = (IWeaponStats)typeof(WeaponBase).GetField("stats", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(weapon);
                Assert.AreEqual(32.5f, stats.Damage, 0.001f);
                Assert.AreEqual(2.5f, stats.ParalysisDuration, 0.001f);
                Assert.AreEqual(1, data.baseStats.paralysisChance);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void ExplosionUpgrades_KeepDirectDamageSeparateAndScaleRadius()
        {
            IWeaponStats stats = new BaseWeaponStats(new WeaponBaseStats { baseDamage = 12, explosionDamageRatio = 1, explosionRadius = 0.8f });
            stats = new ExplosionUpgrade(stats, 80, false);
            stats = new ExplosionUpgrade(stats, 80, true);
            Assert.AreEqual(12, stats.Damage);
            Assert.AreEqual(21.6f, stats.ExplosionDamage, 0.001f);
            Assert.AreEqual(1.44f, stats.ExplosionRadius, 0.001f);
            stats = new DamageUpgrade(stats, -30);
            Assert.AreEqual(8.4f, stats.Damage, 0.001f);
            Assert.AreEqual(15.12f, stats.ExplosionDamage, 0.001f);
        }

        [Test]
        public void Explosion_HitsBoundaryAndDeduplicatesTargets()
        {
            var provider = new Provider();
            var inside = new Target();
            var boundary = new Target { Position = Vector2.right };
            var outside = new Target { Position = Vector2.right * 1.01f };
            provider.Targets.AddRange(new IEnemyTarget[] { inside, inside, boundary, outside });
            Assert.AreEqual(2, AreaDamage.Apply(provider, Vector2.zero, 1, 10));
            Assert.AreEqual(10, inside.Damage);
            Assert.AreEqual(10, boundary.Damage);
            Assert.AreEqual(0, outside.Damage);
            Assert.AreEqual(0, AreaDamage.Apply(provider, Vector2.zero, float.NaN, 10));
        }
    }
}
