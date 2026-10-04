using System.Collections.Generic;
using System.Reflection;
using Game.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Tests
{
    public class LogProjectileTests
    {
        private const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;
        private class Target : IEnemyTarget, IStunnableTarget, ISlowableTarget, IVulnerableTarget
        {
            public Vector2 Position { get; set; }
            public int Hits, Stuns, Slows, Wounds;
            public float SlowDuration;
            public void ApplyStun(float duration) => Stuns++;
            public void ApplySlow(float ratio, float duration) { Slows++; SlowDuration = duration; }
            public void ApplyVulnerability(float ratio, float duration) => Wounds++;

            public void TakeDamage(int value) => Hits++;
        }
        private class Provider : IEnemyTargetProvider
        {
            public Target Target = new();
            public int GetNearest(Vector2 from, int count, List<IEnemyTarget> results)
            { results.Clear(); results.Add(Target); return 1; }
        }
        private static IWeaponStats Stats(WeaponBase weapon) => (IWeaponStats)typeof(WeaponBase).GetField("stats", Flags).GetValue(weapon);

        [TestCase(4, 10.24f, "연발 통나무")]
        [TestCase(5, 16f, "연발 통나무+")]
        public void LogRepeat_UsesPermanentVariantAndSameTwoPickCounter(int permanent, float damage, string name)
        {
            var go = new GameObject("LogRepeatTest");
            try
            {
                var data = new DefaultWeaponDataProvider().LoadAll().Find(w => w.id == 5);
                Assert.AreEqual(10, data.maxLevel); Assert.IsNull(data.progressionId);
                var weapon = new ProjectileCaster(data, go, go.transform, new Provider());
                Assert.AreEqual(0, weapon.UpgradeCount);
                weapon.LevelUp(data.upgrades.Find(c => c.id == "log_damage"));
                var repeat = data.upgrades.Find(c => c.id == "log_repeat");
                Assert.IsTrue(weapon.LevelUp(repeat, permanent)); Assert.IsTrue(weapon.LevelUp(repeat, permanent));
                Assert.IsFalse(weapon.LevelUp(repeat, permanent));
                Assert.AreEqual(3, Stats(weapon).CastCount); Assert.AreEqual(damage, Stats(weapon).Damage, .001f);
                Assert.AreEqual(2, weapon.GetAcquiredCount("log_repeat")); Assert.AreEqual(3, weapon.UpgradeCount);
                Assert.IsTrue(WeaponUpgradeResolver.TryResolve(repeat, permanent, out var resolved)); Assert.AreEqual(name, resolved.Name);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void LogBasicUpgrades_KeepDamageSizeSpeedAndCooldownSeparate()
        {
            var go = new GameObject("LogBasicTest");
            try
            {
                var data = new DefaultWeaponDataProvider().LoadAll().Find(w => w.id == 5);
                var weapon = new ProjectileCaster(data, go, go.transform, new Provider());
                var damage = data.upgrades.Find(c => c.id == "log_damage");
                Assert.IsTrue(weapon.LevelUp(damage)); Assert.IsTrue(weapon.LevelUp(damage)); Assert.IsFalse(weapon.LevelUp(damage));
                Assert.IsTrue(weapon.LevelUp(data.upgrades.Find(c => c.id == "log_size")));
                Assert.IsTrue(weapon.LevelUp(data.upgrades.Find(c => c.id == "log_speed")));
                var stats = Stats(weapon);
                Assert.AreEqual(25.6f, stats.Damage, .001f); Assert.AreEqual(1.6f, stats.ProjectileSizeMultiplier, .001f);
                Assert.AreEqual(28, stats.ProjectileSpeed, .001f); Assert.AreEqual(.75f, stats.Cooldown);
                Assert.AreEqual(1, stats.CastCount); Assert.AreEqual(0, stats.ParalysisDuration);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void SizeUpgrade_ExpandsVisualAndSweptHitRadiusAndInitRestoresDefaultRadius()
        {
            var go = new GameObject("LogSizeTest"); go.AddComponent<Projectile>();
            try
            {
                var data = new DefaultWeaponDataProvider().LoadAll().Find(w => w.id == 5);
                var provider = new Provider(); provider.Target.Position = Vector2.up * 2;
                var weapon = new ProjectileCaster(data, go, go.transform, provider);
                weapon.LevelUp(data.upgrades.Find(c => c.id == "log_size"));
                typeof(ProjectileCaster).GetMethod("OnFire", Flags).Invoke(weapon, null);
                Projectile clone = null;
                foreach (var projectile in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
                { if (projectile.name == "LogSizeTest(Clone)") { clone = projectile; } }
                Assert.AreEqual(Vector3.one * 1.6f, clone.transform.localScale);
                Assert.AreEqual(.48f, (float)typeof(Projectile).GetField("hitRadius", Flags).GetValue(clone), .001f);
                provider.Target.Position = new Vector2(.4f, .5f);
                typeof(Projectile).GetMethod("Tick", Flags).Invoke(clone, new object[] { .1f });
                Assert.AreEqual(1, provider.Target.Hits);
                var pool = (UnityEngine.Pool.IObjectPool<Projectile>)typeof(Projectile).GetField("pool", Flags).GetValue(clone);
                var reused = pool.Get(); Assert.AreSame(clone, reused);
                reused.Init(pool, Vector3.zero, Vector3.up, 10, 20, 3, provider);
                Assert.AreEqual(.3f, (float)typeof(Projectile).GetField("hitRadius", Flags).GetValue(reused));
                typeof(Projectile).GetMethod("Tick", Flags).Invoke(reused, new object[] { .1f });
                Assert.AreEqual(1, provider.Target.Hits);
            }
            finally
            {
                foreach (var projectile in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
                { if (projectile.name == "LogSizeTest(Clone)") { Object.DestroyImmediate(projectile.gameObject); } }
                Object.DestroyImmediate(go);
            }
        }

        [TestCase(.099f, 1)]
        [TestCase(.1f, 0)]
        public void LogImpact_UsesTenPercentStunAndResetsAllNewStatusesOnReuse(float roll, int stuns)
        {
            var go = new GameObject("LogStatusProcTest");
            try
            {
                var projectile = go.AddComponent<Projectile>(); var provider = new Provider();
                var pool = new UnityEngine.Pool.ObjectPool<Projectile>(() => projectile);
                pool.Get().Init(pool, Vector3.zero, Vector3.up, 10, 0, 3, provider,
                    randomValue: () => roll, stunDuration: 1, stunChance: .1f,
                    slowRatio: .3f, slowDuration: 6, vulnerabilityRatio: .2f, vulnerabilityDuration: 6);
                typeof(Projectile).GetMethod("Tick", Flags).Invoke(projectile, new object[] { .1f });
                Assert.AreEqual(stuns, provider.Target.Stuns); Assert.AreEqual(1, provider.Target.Slows);
                Assert.AreEqual(1, provider.Target.Wounds); Assert.AreEqual(6, provider.Target.SlowDuration);
                pool.Get().Init(pool, Vector3.zero, Vector3.up, 10, 0, 3, provider);
                typeof(Projectile).GetMethod("Tick", Flags).Invoke(projectile, new object[] { .1f });
                Assert.AreEqual(stuns, provider.Target.Stuns); Assert.AreEqual(1, provider.Target.Slows); Assert.AreEqual(1, provider.Target.Wounds);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void ActualLogCaster_SnapshotsStunSlowWoundAndKnockbackFromCatalog()
        {
            var go = new GameObject("LogStatusCasterTest"); go.AddComponent<Projectile>();
            try
            {
                var data = new DefaultWeaponDataProvider().LoadAll().Find(w => w.id == 5);
                var provider = new Provider(); provider.Target.Position = Vector2.up;
                var weapon = new ProjectileCaster(data, go, go.transform, provider);
                foreach (var id in new[] { "log_impact", "log_weight", "log_wound" })
                { Assert.IsTrue(weapon.LevelUp(data.upgrades.Find(c => c.id == id))); Assert.IsFalse(weapon.LevelUp(data.upgrades.Find(c => c.id == id))); }
                typeof(ProjectileCaster).GetMethod("OnFire", Flags).Invoke(weapon, null);
                Projectile clone = null;
                foreach (var projectile in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
                { if (projectile.name == "LogStatusCasterTest(Clone)") { clone = projectile; } }
                Assert.AreEqual(.35f, (float)typeof(Projectile).GetField("knockbackDistance", Flags).GetValue(clone), .001f);
                Assert.AreEqual(1, (float)typeof(Projectile).GetField("stunDuration", Flags).GetValue(clone));
                Assert.AreEqual(.1f, (float)typeof(Projectile).GetField("stunChance", Flags).GetValue(clone));
                Assert.AreEqual(6, (float)typeof(Projectile).GetField("slowDuration", Flags).GetValue(clone));
                Assert.AreEqual(.2f, (float)typeof(Projectile).GetField("vulnerabilityRatio", Flags).GetValue(clone), .001f);
                Assert.AreEqual(6, (float)typeof(Projectile).GetField("vulnerabilityDuration", Flags).GetValue(clone));
                Assert.AreEqual(10, Stats(weapon).Damage);
            }
            finally
            {
                foreach (var projectile in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
                { if (projectile.name == "LogStatusCasterTest(Clone)") { Object.DestroyImmediate(projectile.gameObject); } }
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ActualPlayerPrefab_HasIdFiveProjectileForLogAcquisition()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player/Player_Animated.prefab");
            var controller = prefab.GetComponent<WeaponController>();
            var entries = new SerializedObject(controller).FindProperty("prefabEntries");
            bool found = false;
            for (int i = 0; i < entries.arraySize; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("id").intValue != 5) { continue; }
                var log = (GameObject)entry.FindPropertyRelative("prefab").objectReferenceValue;
                Assert.IsNotNull(log.GetComponent<Projectile>()); found = true;
            }
            Assert.IsTrue(found);
        }
    }
}
