using System;
using System.Collections.Generic;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class WeaponStatsSnapshotTests
    {
        private static WeaponStats Baseline() => WeaponStats.FromDefinition(new WeaponBaseStats
        {
            baseDamage = 100,
            cooldown = 2,
            speed = 10,
            hitCount = 2,
            castCount = 1,
            pierceCount = 1,
            freezeDuration = 1,
            paralysisDuration = 1,
            stunDuration = 1,
            slowDuration = 1,
            knockbackDistance = 1,
            explosionRadius = 2,
            explosionDamageRatio = .5f,
            fieldDamageRatio = .5f,
            fieldRadius = 2,
            fieldSlowRatio = .3f
        });

        private static IEnumerable<TestCaseData> EffectCases()
        {
            yield return Case(UpgradeType.AttackSpeed, 20, s => s.Cast.Cooldown, 1.6f);
            yield return Case(UpgradeType.Damage, 20, s => s.Cast.Damage, 120);
            yield return Case(UpgradeType.ImpactDamage, 20, s => s.Cast.Damage, 120);
            yield return Case(UpgradeType.ProjectileCount, 1.6f, s => s.Cast.ProjectileCount, 4);
            yield return Case(UpgradeType.HitCount, 1.6f, s => s.Cast.ProjectileCount, 3);
            yield return Case(UpgradeType.CastCount, 2, s => s.Cast.Count, 3);
            yield return Case(UpgradeType.ReserveCasts, 2, s => s.Cast.ReserveCount, 2);
            yield return Case(UpgradeType.Form, 3, s => (int)s.Cast.Form, 3);
            yield return Case(UpgradeType.PierceCount, 2, s => s.Projectile.PierceCount, 3);
            yield return Case(UpgradeType.ProjectileSpeed, 20, s => s.Projectile.Speed, 12);
            yield return Case(UpgradeType.ProjectileSize, 20, s => s.Projectile.SizeMultiplier, 1.2f);
            yield return Case(UpgradeType.Knockback, 20, s => s.Projectile.KnockbackDistance, 1.2f);
            yield return Case(UpgradeType.EnableExplosion, 3, s => s.Explosion.Radius, 3);
            yield return Case(UpgradeType.ExplosionDamage, 20, s => s.Explosion.Damage, 60);
            yield return Case(UpgradeType.ExplosionRadius, 20, s => s.Explosion.Radius, 2.4f);
            yield return Case(UpgradeType.FreezeDuration, 2, s => s.Status.FreezeDuration, 2);
            yield return Case(UpgradeType.Frostbite, 20, s => s.Status.FrostbiteRatio, .2f);
            yield return Case(UpgradeType.Paralysis, 2, s => s.Status.ParalysisDuration, 2);
            yield return Case(UpgradeType.ParalysisDuration, 2, s => s.Status.ParalysisDuration, 3);
            yield return Case(UpgradeType.StunDuration, 2, s => s.Status.StunDuration, 2);
            yield return Case(UpgradeType.SlowDuration, 2, s => s.Status.SlowDuration, 3);
            yield return Case(UpgradeType.VulnerabilityRatio, 20, s => s.Status.VulnerabilityRatio, .2f);
            yield return Case(UpgradeType.VulnerabilityDuration, 2, s => s.Status.VulnerabilityDuration, 2);
            yield return Case(UpgradeType.BurnDuration, 2, s => s.Burn.Duration, 2);
            yield return Case(UpgradeType.BurnRatio, 20, s => s.Burn.DamageRatio, .2f);
            yield return Case(UpgradeType.BurnMaxHp, 20, s => s.Burn.MaxHpRatio, .2f);
            yield return Case(UpgradeType.BurnDeathExplosion, 1, s => s.Burn.DeathExplosion ? 1 : 0, 1);
            yield return Case(UpgradeType.SplitCount, 2, s => s.Secondary.Count, 2);
            yield return Case(UpgradeType.SplitDamage, 20, s => s.Secondary.DamageMultiplier, 1.2f);
            yield return Case(UpgradeType.SplitFrostbite, 20, s => s.Secondary.FrostbiteRatio, .2f);
            yield return Case(UpgradeType.LightningStrike, 20, s => s.Secondary.LightningStrikeRatio, .2f);
            yield return Case(UpgradeType.SplitLightning, 20, s => s.Secondary.LightningRatio, .2f);
            yield return Case(UpgradeType.SplitParalysis, 2, s => s.Secondary.ParalysisDuration, 2);
            yield return Case(UpgradeType.SplitExplosion, 1, s => s.Secondary.Explosions ? 1 : 0, 1);
            yield return Case(UpgradeType.KillLightning, 20, s => s.Secondary.KillLightningRatio, .2f);
            yield return Case(UpgradeType.FieldDuration, 2, s => s.Field.Duration, 2);
            yield return Case(UpgradeType.FieldDamageFlat, 2, s => s.Field.FlatDamage, 2);
            yield return Case(UpgradeType.FieldDamageMultiplier, 20, s => s.Field.DamageMultiplier, 1.2f);
        }

        private static TestCaseData Case(UpgradeType type, float value, Func<WeaponStats, float> read, float expected) =>
            new TestCaseData(type, value, read, expected).SetName("SnapshotEffect_" + type);

        [TestCaseSource(nameof(EffectCases))]
        public void Effects_PreserveCombatValues(UpgradeType type, float value, Func<WeaponStats, float> read, float expected)
        {
            var current = Baseline();
            Assert.IsTrue(WeaponStatEffects.TryApply(current, new[] { new StatEffect { type = type, value = value } }, out var result));
            Assert.AreEqual(expected, read(result), .0001f);
            Assert.AreEqual(100, current.Cast.Damage);
            Assert.AreEqual(2, current.Cast.Cooldown);
        }

        [Test]
        public void Snapshot_DetachesFromMutableDefinition()
        {
            var data = new WeaponBaseStats { baseDamage = 100, explosionDamageRatio = .5f };
            var snapshot = WeaponStats.FromDefinition(data);
            data.baseDamage = 900;
            Assert.AreEqual(100, snapshot.Cast.Damage);
            Assert.AreEqual(50, snapshot.Explosion.Damage);
        }

        [Test]
        public void OrderedEffects_KeepImpactAndExplosionDamageIndependent()
        {
            Assert.IsTrue(WeaponStatEffects.TryApply(Baseline(), new[]
            {
                new StatEffect { type = UpgradeType.ImpactDamage, value = 80 },
                new StatEffect { type = UpgradeType.ExplosionDamage, value = 80 },
                new StatEffect { type = UpgradeType.Damage, value = 20 },
                new StatEffect { type = UpgradeType.Damage, value = -30 }
            }, out var result));
            Assert.AreEqual(151.2f, result.Cast.Damage, .001f);
            Assert.AreEqual(75.6f, result.Explosion.Damage, .001f);
        }

        [TestCase(UpgradeType.SplitCount, 1.5f)]
        [TestCase(UpgradeType.ReserveCasts, 0)]
        [TestCase(UpgradeType.Form, 0)]
        [TestCase(UpgradeType.BurnDeathExplosion, 2)]
        [TestCase(UpgradeType.SplitExplosion, 2)]
        [TestCase(UpgradeType.FreezeDuration, -1)]
        [TestCase(UpgradeType.Damage, float.NaN)]
        [TestCase(UpgradeType.Damage, float.PositiveInfinity)]
        [TestCase((UpgradeType)999, 1)]
        public void InvalidEffect_DiscardsWholePreparation(UpgradeType type, float value)
        {
            var current = Baseline();
            Assert.IsFalse(WeaponStatEffects.TryApply(current, new[]
            {
                new StatEffect { type = UpgradeType.Damage, value = 80 },
                new StatEffect { type = type, value = value }
            }, out var result));
            Assert.IsNull(result);
            Assert.AreEqual(100, current.Cast.Damage);
            Assert.AreEqual(50, current.Explosion.Damage);
        }

        private sealed class Target : IEnemyTarget
        {
            public Vector2 Position { get; set; }
            public void TakeDamage(int damage) { }
        }

        private sealed class Targets : IEnemyTargetProvider
        {
            public readonly List<IEnemyTarget> Values = new();
            public int Requested;
            public int GetNearest(Vector2 from, int count, List<IEnemyTarget> results)
            {
                Requested = count;
                results.Clear();
                results.AddRange(Values);
                return results.Count;
            }
        }

        [Test]
        public void TargetSelection_FiltersRangeAndKeepsWallPriority()
        {
            var provider = new Targets();
            var nearWall = new Target { Position = new Vector2(2, 1) };
            var nearCaster = new Target { Position = new Vector2(0, 2) };
            provider.Values.AddRange(new IEnemyTarget[] { nearCaster, new Target { Position = new Vector2(100, 0) }, nearWall });
            var selector = new WeaponTargetSelector(provider);
            var result = selector.Select(Vector2.zero, 3);
            Assert.AreEqual(8, provider.Requested);
            CollectionAssert.AreEqual(new IEnemyTarget[] { nearWall, nearCaster }, result);
            provider.Values.Clear();
            Assert.IsEmpty(selector.Select(Vector2.zero, 3));
        }
    }
}
