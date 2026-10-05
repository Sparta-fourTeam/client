using System.Collections.Generic;
using System.Reflection;
using Game.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Pool;
using Object = UnityEngine.Object;

namespace Game.Tests
{
    public class ElectromagneticFieldTests
    {
        private const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;
        private class Target : IEnemyTarget, IAreaSlowTarget
        {
            public Vector2 Position { get; set; }
            public int DamageTaken;
            public readonly Dictionary<object, float> Slows = new();
            public void TakeDamage(int damage) => DamageTaken += damage;
            public void SetAreaSlow(object source, float ratio) => Slows[source] = ratio;
            public void RemoveAreaSlow(object source) => Slows.Remove(source);
        }
        private class Provider : IEnemyTargetProvider
        {
            public readonly List<IEnemyTarget> Targets = new();
            public int GetNearest(Vector2 from, int count, List<IEnemyTarget> results)
            { results.Clear(); results.AddRange(Targets); return results.Count; }
        }
        private static void Tick(ElectromagneticField field, float dt) => typeof(ElectromagneticField).GetMethod("Tick", Flags).Invoke(field, new object[] { dt });

        [Test]
        public void Field_DeduplicatesTicksIncludesBoundaryAndReleasesSlowAtExpiry()
        {
            var go = new GameObject("FieldTickTest");
            try
            {
                var field = go.AddComponent<ElectromagneticField>();
                var inside = new Target(); var boundary = new Target { Position = Vector2.right * .8f };
                var outside = new Target { Position = Vector2.right * .81f };
                var provider = new Provider(); provider.Targets.AddRange(new IEnemyTarget[] { inside, inside, boundary, outside });
                var pool = new ObjectPool<ElectromagneticField>(() => field, actionOnGet: f => f.gameObject.SetActive(true), actionOnRelease: f => f.gameObject.SetActive(false));
                pool.Get().Init(pool, provider, Vector2.zero, .8f, 2.5f, 3, .3f);
                Assert.AreEqual(1, inside.Slows.Count, "inside");
                Assert.AreEqual(1, boundary.Slows.Count, "boundary");
                Assert.AreEqual(0, outside.Slows.Count, "outside");
                Tick(field, .5f); Assert.AreEqual(0, inside.DamageTaken);
                Tick(field, 10);
                Assert.AreEqual(6, inside.DamageTaken); Assert.AreEqual(6, boundary.DamageTaken); Assert.AreEqual(0, outside.DamageTaken);
                Assert.AreEqual(0, inside.Slows.Count); Assert.IsFalse(go.activeSelf); Assert.AreEqual(1, pool.CountInactive);
                Tick(field, 10); Assert.AreEqual(6, inside.DamageTaken);
                // Reuse clears elapsed time and snapshots different damage/duration.
                pool.Get().Init(pool, provider, Vector2.zero, .8f, 10, 1, .3f);
                Tick(field, .5f); Assert.AreEqual(6, inside.DamageTaken);
                Tick(field, .5f); Assert.AreEqual(16, inside.DamageTaken);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Field_FrameSizedTicksPreserveFinalDamageTick()
        {
            var go = new GameObject("FieldFrameTickTest");
            try
            {
                var field = go.AddComponent<ElectromagneticField>();
                var target = new Target(); var provider = new Provider(); provider.Targets.Add(target);
                var pool = new ObjectPool<ElectromagneticField>(() => field);
                pool.Get().Init(pool, provider, Vector2.zero, .8f, 10, 3, .3f);
                for (int i = 0; i < 181; i++) { Tick(field, 1f / 60); }
                Assert.AreEqual(30, target.DamageTaken); Assert.AreEqual(0, target.Slows.Count);
                Assert.AreEqual(1, pool.CountInactive);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Field_EntryExitOverlapAndDisableRemoveOnlyOwnSlow()
        {
            var firstGo = new GameObject("FieldFirstTest"); var secondGo = new GameObject("FieldSecondTest");
            try
            {
                var first = firstGo.AddComponent<ElectromagneticField>(); var second = secondGo.AddComponent<ElectromagneticField>();
                var target = new Target { Position = Vector2.right * 2 };
                var provider = new Provider(); provider.Targets.Add(target);
                var poolA = new ObjectPool<ElectromagneticField>(() => first); var poolB = new ObjectPool<ElectromagneticField>(() => second);
                poolA.Get().Init(poolA, provider, Vector2.zero, 1, 10, 3, .3f);
                poolB.Get().Init(poolB, provider, Vector2.zero, 1, 10, 5, .6f);
                target.Position = Vector2.zero; Tick(first, 0); Tick(second, 0);
                Assert.AreEqual(2, target.Slows.Count);
                firstGo.SetActive(false);
                typeof(ElectromagneticField).GetMethod("OnDisable", Flags).Invoke(first, null);
                Assert.AreEqual(1, target.Slows.Count); Assert.IsTrue(target.Slows.ContainsKey(second));
                target.Position = Vector2.right * 2; Tick(second, 0); Assert.AreEqual(0, target.Slows.Count);
                target.Position = Vector2.zero; Tick(second, 0); Assert.AreEqual(1, target.Slows.Count);
                typeof(ElectromagneticField).GetMethod("OnDestroy", Flags).Invoke(second, null);
                Object.DestroyImmediate(secondGo); Assert.AreEqual(0, target.Slows.Count);
            }
            finally { Object.DestroyImmediate(firstGo); if (secondGo != null) { Object.DestroyImmediate(secondGo); } }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FieldUpgrades_FlatDamageBeforeMultiplierRegardlessOfSelectionOrder(bool voltageFirst)
        {
            var go = new GameObject("FieldStatsTest");
            try
            {
                var data = new DefaultWeaponDataProvider(new GameDataStore()).LoadAll().Find(w => w.id == 3);
                var weapon = Casters.Hitscan(data, go, go.transform, new Provider());
                Assert.IsTrue(weapon.LevelUp(data.upgrades.Find(c => c.id == "lightning_field")));
                string first = voltageFirst ? "lightning_field_voltage" : "lightning_field_stable";
                string second = voltageFirst ? "lightning_field_stable" : "lightning_field_voltage";
                Assert.IsTrue(weapon.LevelUp(data.upgrades.Find(c => c.id == first)));
                Assert.IsTrue(weapon.LevelUp(data.upgrades.Find(c => c.id == second)));
                Assert.IsFalse(weapon.LevelUp(data.upgrades.Find(c => c.id == first)));
                var stats = (WeaponStats)typeof(WeaponBase).GetField("stats", Flags).GetValue(weapon);
                Assert.AreEqual(262.5f, (stats.Cast.Damage * stats.Field.DamageRatio + stats.Field.FlatDamage) * stats.Field.DamageMultiplier);
                Assert.AreEqual(5, stats.Field.Duration); Assert.AreEqual(25, stats.Cast.Damage); Assert.AreEqual(.8f, stats.Field.Radius);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void ActualCaster_CreatesFieldAtCastPositionWithDamageSnapshotAndNoOtherProcs()
        {
            var go = new GameObject("FieldCasterTest"); go.AddComponent<HitscanEffect>();
            try
            {
                var data = new DefaultWeaponDataProvider(new GameDataStore()).LoadAll().Find(w => w.id == 3);
                var provider = new Provider(); var target = new Target { Position = Vector2.up * 2 }; provider.Targets.Add(target);
                var weapon = Casters.Hitscan(data, go, go.transform, provider);
                weapon.LevelUp(data.upgrades.Find(c => c.id == "lightning_field"));
                typeof(SkillCaster).GetMethod("OnFire", Flags).Invoke(weapon, null);
                var fields = Object.FindObjectsByType<ElectromagneticField>(FindObjectsSortMode.None);
                Assert.AreEqual(1, fields.Length); var field = fields[0];
                Assert.AreEqual((Vector3)target.Position, field.transform.position);
                weapon.LevelUp(data.upgrades.Find(c => c.id == "lightning_field_stable"));
                weapon.LevelUp(data.upgrades.Find(c => c.id == "lightning_field_voltage"));
                Tick(field, 1); Assert.AreEqual(2, target.DamageTaken);
                Assert.AreEqual(1, target.Slows.Count);
                int hitscans = 0;
                foreach (var effect in Object.FindObjectsByType<HitscanEffect>(FindObjectsSortMode.None))
                { if (effect.name == "FieldCasterTest(Clone)") { hitscans++; } }
                Assert.AreEqual(1, hitscans);
                Assert.IsNull(field.GetComponent<LineRenderer>());
                var fieldVisual = field.GetComponentInChildren<ParticleSystem>();
                Assert.IsNotNull(fieldVisual); Assert.IsTrue(fieldVisual.main.loop);
                Assert.IsNotNull(fieldVisual.GetComponent<ParticleSystemRenderer>().sharedMaterial.GetTexture("_BaseMap"));
            }
            finally
            {
                foreach (var field in Object.FindObjectsByType<ElectromagneticField>(FindObjectsSortMode.None)) { Object.DestroyImmediate(field.gameObject); }
                foreach (var effect in Object.FindObjectsByType<HitscanEffect>(FindObjectsSortMode.None))
                { if (effect.name == "FieldCasterTest(Clone)") { Object.DestroyImmediate(effect.gameObject); } }
                Object.DestroyImmediate(go);
            }
        }
    }
}
