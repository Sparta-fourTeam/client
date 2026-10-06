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
                pool.Get().Init(pool, Vector3.zero, 10,
                    reactions: new HitReactionBuilder().On(AttackEvent.Impact, new CastSkillReaction(_ => count++)).Build());
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
            var data = new DefaultSkillDataProvider(new GameDataStore()).LoadAll().Find(w => w.id == 2);
            SkillStats stats = SkillStats.FromDefinition(data.baseStats);
            var impact = data.upgrades.Find(c => c.id == "fireball_impact_damage");
            Assert.AreEqual(3, impact.maxPickCount);
            stats = SkillTestFactory.Apply(stats, "impactDamage", impact.effects[0].value);
            Assert.AreEqual(21.6f, stats.Cast.Damage, 0.001f);
            Assert.AreEqual(12, stats.Explosion.Damage, 0.001f);
            stats = SkillTestFactory.Apply(stats, "explosionDamage", 80);
            Assert.AreEqual(21.6f, stats.Cast.Damage, 0.001f);
            Assert.AreEqual(21.6f, stats.Explosion.Damage, 0.001f);
            stats = SkillTestFactory.Apply(SkillTestFactory.Apply(stats, "damage", 20), "attackSpeed", 10);
            Assert.AreEqual(25.92f, stats.Cast.Damage, 0.001f);
            Assert.AreEqual(25.92f, stats.Explosion.Damage, 0.001f);
            Assert.AreEqual(1.62f, stats.Cast.Cooldown, 0.001f);
        }

        [Test]
        public void HighVoltageLightning_IncreasesDamageAndAddsParalysisDuration()
        {
            var data = new DefaultSkillDataProvider(new GameDataStore()).LoadAll().Find(w => w.id == 3);
            var go = new GameObject("HighVoltageStatsTest");
            try
            {
                var weapon = Casters.Hitscan(data, go, go.transform, new Provider());
                var option = data.upgrades.Find(c => c.id == "lightning_voltage");
                Assert.IsTrue(weapon.LevelUp(option));
                Assert.IsFalse(weapon.LevelUp(option));
                var stats = (SkillStats)typeof(SkillBase).GetField("stats", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(weapon);
                Assert.AreEqual(32.5f, stats.Cast.Damage, 0.001f);
                Assert.AreEqual(2.5f, stats.Status.ParalysisDuration, 0.001f);
                Assert.AreEqual(1, data.baseStats.status.paralysisChance);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void JudgementCard_ChangesFormAndPooledVisualScaleResets()
        {
            var go = new GameObject("JudgementScaleTest"); go.AddComponent<HitscanEffect>();
            go.transform.position = new Vector3(1000, 1000, 0);
            try
            {
                var data = new DefaultSkillDataProvider(new GameDataStore()).LoadAll().Find(w => w.id == 3);
                var provider = new Provider(); provider.Targets.Add(new Target { Position = new Vector2(1000, 1000) });
                var weapon = Casters.Hitscan(data, go, go.transform, provider);
                Assert.IsTrue(weapon.LevelUp(data.upgrades.Find(c => c.id == "lightning_judgement")));
                Assert.IsFalse(weapon.LevelUp(data.upgrades.Find(c => c.id == "lightning_judgement")));
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                var statsField = typeof(SkillBase).GetField("stats", flags);
                Assert.AreEqual(75, ((SkillStats)statsField.GetValue(weapon)).Cast.Damage);
                var fire = typeof(SkillCaster).GetMethod("OnFire", flags);
                fire.Invoke(weapon, null);
                HitscanEffect clone = null;
                foreach (var effect in Object.FindObjectsByType<HitscanEffect>(FindObjectsSortMode.None))
                { if (effect.name == "JudgementScaleTest(Clone)") { clone = effect; } }
                Assert.AreEqual(Vector3.one * 1.5f, clone.transform.localScale);
                typeof(HitscanEffect).GetMethod("Release", flags).Invoke(clone, null);
                statsField.SetValue(weapon, SkillStats.FromDefinition(data.baseStats));
                fire.Invoke(weapon, null);
                Assert.AreEqual(Vector3.one, clone.transform.localScale);
            }
            finally
            {
                foreach (var effect in Object.FindObjectsByType<HitscanEffect>(FindObjectsSortMode.None))
                { if (effect.name == "JudgementScaleTest(Clone)") { Object.DestroyImmediate(effect.gameObject); } }
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void JudgementForm_PreservesOtherStatsAndScalesDamage()
        {
            SkillStats stats = SkillStats.FromDefinition(new SkillBaseStats { cast = { baseDamage = 25 }, status = { paralysisDuration = 1 } });
            stats = SkillTestFactory.Apply(stats, "damage", 150);
            stats = SkillTestFactory.Apply(SkillTestFactory.Apply(stats, "damage", 200), "form", (int)SkillForm.JudgementThunder);
            Assert.AreEqual(187.5f, stats.Cast.Damage);
            Assert.AreEqual(1, stats.Status.ParalysisDuration);
            Assert.AreEqual(SkillForm.JudgementThunder, stats.Cast.Form);
        }

        [Test]
        public void ExplosionUpgrades_KeepDirectDamageSeparateAndScaleRadius()
        {
            SkillStats stats = SkillStats.FromDefinition(new SkillBaseStats { cast = { baseDamage = 12 }, explosion = { damageRatio = 1, radius = 0.8f } });
            stats = SkillTestFactory.Apply(stats, "explosionDamage", 80);
            stats = SkillTestFactory.Apply(stats, "explosionRadius", 80);
            Assert.AreEqual(12, stats.Cast.Damage);
            Assert.AreEqual(21.6f, stats.Explosion.Damage, 0.001f);
            Assert.AreEqual(1.44f, stats.Explosion.Radius, 0.001f);
            stats = SkillTestFactory.Apply(stats, "damage", -30);
            Assert.AreEqual(8.4f, stats.Cast.Damage, 0.001f);
            Assert.AreEqual(15.12f, stats.Explosion.Damage, 0.001f);
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
