using System;
using System.Collections.Generic;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class SkillStatsSnapshotTests
    {
        private static SkillStats Baseline() => SkillStats.FromDefinition(new SkillBaseStats
        {
            cast = { baseDamage = 100, cooldown = 2, projectileCount = 2, castCount = 1 },
            projectile = { speed = 10, pierceCount = 1, knockbackDistance = 1 },
            status = { freezeDuration = 1, paralysisDuration = 1, stunDuration = 1, slowDuration = 1 },
            explosion = { radius = 2, damageRatio = .5f },
            field = { damageRatio = .5f, radius = 2, slowRatio = .3f }
        });

        private static IEnumerable<TestCaseData> EffectCases()
        {
            yield return Case("attackSpeed", 20, s => s.Cast.Cooldown, 1.6f);
            yield return Case("damage", 20, s => s.Cast.Damage, 120);
            yield return Case("impactDamage", 20, s => s.Cast.Damage, 120);
            yield return Case("projectileCount", 1.6f, s => s.Cast.ProjectileCount, 4);
            yield return Case("castCount", 2, s => s.Cast.Count, 3);
            yield return Case("reserveCasts", 2, s => s.Cast.ReserveCount, 2);
            yield return Case("form", 3, s => (int)s.Cast.Form, 3);
            yield return Case("pierceCount", 2, s => s.Projectile.PierceCount, 3);
            yield return Case("projectileSpeed", 20, s => s.Projectile.Speed, 12);
            yield return Case("projectileSize", 20, s => s.Projectile.SizeMultiplier, 1.2f);
            yield return Case("knockback", 20, s => s.Projectile.KnockbackDistance, 1.2f);
            yield return Case("enableExplosion", 3, s => s.Explosion.Radius, 3);
            yield return Case("explosionDamage", 20, s => s.Explosion.Damage, 60);
            yield return Case("explosionRadius", 20, s => s.Explosion.Radius, 2.4f);
            yield return Case("freezeDuration", 2, s => s.Status.FreezeDuration, 2);
            yield return Case("frostbite", 20, s => s.Status.FrostbiteRatio, .2f);
            yield return Case("paralysis", 2, s => s.Status.ParalysisDuration, 2);
            yield return Case("paralysisDuration", 2, s => s.Status.ParalysisDuration, 3);
            yield return Case("stunDuration", 2, s => s.Status.StunDuration, 2);
            yield return Case("slowDuration", 2, s => s.Status.SlowDuration, 3);
            yield return Case("vulnerabilityRatio", 20, s => s.Status.VulnerabilityRatio, .2f);
            yield return Case("vulnerabilityDuration", 2, s => s.Status.VulnerabilityDuration, 2);
            yield return Case("burnDuration", 2, s => s.Burn.Duration, 2);
            yield return Case("burnRatio", 20, s => s.Burn.DamageRatio, .2f);
            yield return Case("burnMaxHp", 20, s => s.Burn.MaxHpRatio, .2f);
            yield return Case("burnDeathExplosion", 1, s => s.Burn.DeathExplosion ? 1 : 0, 1);
            yield return Case("lightningStrike", 20, s => s.Lightning.StrikeRatio, .2f);
            yield return Case("killLightning", 20, s => s.Lightning.KillRatio, .2f);
            yield return Case("fieldDuration", 2, s => s.Field.Duration, 2);
            yield return Case("fieldDamageFlat", 2, s => s.Field.FlatDamage, 2);
            yield return Case("fieldDamageMultiplier", 20, s => s.Field.DamageMultiplier, 1.2f);
        }

        private static TestCaseData Case(string type, float value, Func<SkillStats, float> read, float expected) =>
            new TestCaseData(type, value, read, expected).SetName("SnapshotEffect_" + type);

        [TestCaseSource(nameof(EffectCases))]
        public void Effects_PreserveCombatValues(string type, float value, Func<SkillStats, float> read, float expected)
        {
            var current = Baseline();
            Assert.IsTrue(SkillStatEffects.TryApply(current, new[] { new EffectDef { kind = type, value = value } }, out var result));
            Assert.AreEqual(expected, read(result), .0001f);
            Assert.AreEqual(100, current.Cast.Damage);
            Assert.AreEqual(2, current.Cast.Cooldown);
        }

        [Test]
        public void Snapshot_DetachesFromMutableDefinition()
        {
            var data = new SkillBaseStats { cast = { baseDamage = 100 }, explosion = { damageRatio = .5f } };
            var snapshot = SkillStats.FromDefinition(data);
            data.cast.baseDamage = 900;
            Assert.AreEqual(100, snapshot.Cast.Damage);
            Assert.AreEqual(50, snapshot.Explosion.Damage);
        }

        [Test]
        public void OrderedEffects_KeepImpactAndExplosionDamageIndependent()
        {
            Assert.IsTrue(SkillStatEffects.TryApply(Baseline(), new[]
            {
                new EffectDef { kind = "impactDamage", value = 80 },
                new EffectDef { kind = "explosionDamage", value = 80 },
                new EffectDef { kind = "damage", value = 20 },
                new EffectDef { kind = "damage", value = -30 }
            }, out var result));
            Assert.AreEqual(151.2f, result.Cast.Damage, .001f);
            Assert.AreEqual(75.6f, result.Explosion.Damage, .001f);
        }

        [TestCase("reserveCasts", 0)]
        [TestCase("form", 0)]
        [TestCase("burnDeathExplosion", 2)]
        [TestCase("freezeDuration", -1)]
        [TestCase("damage", float.NaN)]
        [TestCase("damage", float.PositiveInfinity)]
        [TestCase("unknownKind", 1)]
        public void InvalidEffect_DiscardsWholePreparation(string type, float value)
        {
            var current = Baseline();
            Assert.IsFalse(SkillStatEffects.TryApply(current, new[]
            {
                new EffectDef { kind = "damage", value = 80 },
                new EffectDef { kind = type, value = value }
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
            var selector = new SkillTargetSelector(provider);
            var result = selector.Select(Vector2.zero, 3);
            Assert.AreEqual(8, provider.Requested);
            CollectionAssert.AreEqual(new IEnemyTarget[] { nearWall, nearCaster }, result);
            provider.Values.Clear();
            Assert.IsEmpty(selector.Select(Vector2.zero, 3));
        }
    }
}
