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
        private class Target : IEnemyTarget, IStunnableTarget, ISlowableTarget, IVulnerableTarget, IBurnableTarget
        {
            public Vector2 Position { get; set; }
            public int Hits, Stuns, Slows, Wounds;
            public float SlowDuration;
            public int Burns;
            public float BurnDuration, BurnDamage;
            public void ApplyBurn(float damage, float duration, float maxHpRatio = 0, System.Action<Vector2> onDeath = null)
            { Burns++; BurnDuration = duration; BurnDamage = damage; }
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
        private static WeaponStats Stats(WeaponBase weapon) => (WeaponStats)typeof(WeaponBase).GetField("stats", Flags).GetValue(weapon);

        [TestCase(4, 10.24f, "연발 나무뿌리")]
        [TestCase(5, 16f, "연발 나무뿌리+")]
        public void LogRepeat_UsesPermanentVariantAndSameTwoPickCounter(int permanent, float damage, string name)
        {
            var go = new GameObject("LogRepeatTest");
            try
            {
                var data = new DefaultWeaponDataProvider(new GameDataStore()).LoadAll().Find(w => w.id == 5);
                Assert.AreEqual(10, data.maxLevel); Assert.IsNull(data.progressionId);
                var weapon = Casters.Projectile(data, go, go.transform, new Provider());
                Assert.AreEqual(0, weapon.UpgradeCount);
                weapon.LevelUp(data.upgrades.Find(c => c.id == "log_damage"));
                var repeat = data.upgrades.Find(c => c.id == "log_repeat");
                Assert.IsTrue(weapon.LevelUp(repeat, permanent)); Assert.IsTrue(weapon.LevelUp(repeat, permanent));
                Assert.IsFalse(weapon.LevelUp(repeat, permanent));
                Assert.AreEqual(3, Stats(weapon).Cast.Count); Assert.AreEqual(damage, Stats(weapon).Cast.Damage, .001f);
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
                var data = new DefaultWeaponDataProvider(new GameDataStore()).LoadAll().Find(w => w.id == 5);
                var weapon = Casters.Projectile(data, go, go.transform, new Provider());
                var damage = data.upgrades.Find(c => c.id == "log_damage");
                Assert.IsTrue(weapon.LevelUp(damage)); Assert.IsTrue(weapon.LevelUp(damage)); Assert.IsFalse(weapon.LevelUp(damage));
                Assert.IsTrue(weapon.LevelUp(data.upgrades.Find(c => c.id == "log_size")));
                Assert.IsTrue(weapon.LevelUp(data.upgrades.Find(c => c.id == "log_speed")));
                var stats = Stats(weapon);
                Assert.AreEqual(25.6f, stats.Cast.Damage, .001f); Assert.AreEqual(1.6f, stats.Projectile.SizeMultiplier, .001f);
                Assert.AreEqual(5.6f, stats.Projectile.Speed, .001f); Assert.AreEqual(.75f, stats.Cast.Cooldown);
                Assert.AreEqual(1, stats.Cast.Count); Assert.AreEqual(0, stats.Status.ParalysisDuration);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void SizeUpgrade_ExpandsVisualAndSweptHitRadiusAndInitRestoresDefaultRadius()
        {
            var go = new GameObject("LogSizeTest"); go.AddComponent<Projectile>();
            try
            {
                var data = new DefaultWeaponDataProvider(new GameDataStore()).LoadAll().Find(w => w.id == 5);
                var provider = new Provider(); provider.Target.Position = Vector2.up * 2;
                var weapon = Casters.Projectile(data, go, go.transform, provider);
                weapon.LevelUp(data.upgrades.Find(c => c.id == "log_size"));
                typeof(SkillCaster).GetMethod("OnFire", Flags).Invoke(weapon, null);
                Projectile clone = null;
                foreach (var projectile in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
                { if (projectile.name == "LogSizeTest(Clone)") { clone = projectile; } }
                Assert.AreEqual(Vector3.one * 1.6f, clone.transform.localScale);
                Assert.AreEqual(.48f, (float)typeof(Projectile).GetField("hitRadius", Flags).GetValue(clone), .001f);
                provider.Target.Position = new Vector2(.4f, .2f);
                typeof(Projectile).GetMethod("Tick", Flags).Invoke(clone, new object[] { .1f });
                Assert.AreEqual(1, provider.Target.Hits);
                var pool = (UnityEngine.Pool.IObjectPool<Projectile>)typeof(Projectile).GetField("pool", Flags).GetValue(clone);
                pool.Release(clone);
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
                var data = new DefaultWeaponDataProvider(new GameDataStore()).LoadAll().Find(w => w.id == 5);
                var provider = new Provider(); provider.Target.Position = Vector2.up;
                var weapon = Casters.Projectile(data, go, go.transform, provider);
                foreach (var id in new[] { "log_impact", "log_weight", "log_wound" })
                { Assert.IsTrue(weapon.LevelUp(data.upgrades.Find(c => c.id == id))); Assert.IsFalse(weapon.LevelUp(data.upgrades.Find(c => c.id == id))); }
                typeof(SkillCaster).GetMethod("OnFire", Flags).Invoke(weapon, null);
                Projectile clone = null;
                foreach (var projectile in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
                { if (projectile.name == "LogStatusCasterTest(Clone)") { clone = projectile; } }
                var hit = HitRecorder.Hit(clone, .05f);
                Assert.AreEqual(.35f, hit.KnockbackDistance, .001f);
                Assert.AreEqual(1, hit.Stun);
                Assert.AreEqual(0, HitRecorder.Hit(clone, .5f).Stun, "기절 확률은 10%이므로 0.5에서는 걸리지 않는다");
                Assert.AreEqual(6, hit.SlowDuration);
                Assert.AreEqual(.2f, hit.VulnerabilityRatio, .001f);
                Assert.AreEqual(6, hit.VulnerabilityDuration);
                Assert.AreEqual(10, Stats(weapon).Cast.Damage);
            }
            finally
            {
                foreach (var projectile in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
                { if (projectile.name == "LogStatusCasterTest(Clone)") { Object.DestroyImmediate(projectile.gameObject); } }
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void LargeLog_ScalesDamageSizeKnockbackAndPreservesOtherUpgrades()
        {
            var go = new GameObject("LargeLogTest"); go.AddComponent<Projectile>();
            try
            {
                var data = new DefaultWeaponDataProvider(new GameDataStore()).LoadAll().Find(w => w.id == 5);
                var weapon = Casters.Projectile(data, go, go.transform, new Provider());
                foreach (var id in new[] { "log_damage", "log_size", "log_impact", "log_weight", "log_repeat", "log_speed", "log_wound", "log_large" })
                { Assert.IsTrue(weapon.LevelUp(data.upgrades.Find(c => c.id == id), 5)); }
                Assert.IsFalse(weapon.LevelUp(data.upgrades.Find(c => c.id == "log_large")));
                var stats = Stats(weapon);
                Assert.AreEqual(25.6f, stats.Cast.Damage, .001f); Assert.AreEqual(2.56f, stats.Projectile.SizeMultiplier, .001f);
                Assert.AreEqual(.56f, stats.Projectile.KnockbackDistance, .001f); Assert.AreEqual(6, stats.Status.SlowDuration);
                Assert.AreEqual(1, stats.Status.StunDuration); Assert.AreEqual(2, stats.Cast.Count);
                Assert.AreEqual(5.6f, stats.Projectile.Speed, .001f); Assert.AreEqual(.75f, stats.Cast.Cooldown);
                Assert.AreEqual(.2f, stats.Status.VulnerabilityRatio, .001f); Assert.AreEqual(0, stats.Burn.DamageRatio);
                Assert.AreEqual(WeaponForm.LargeLog, stats.Cast.Form);
                typeof(SkillCaster).GetMethod("OnFire", Flags).Invoke(weapon, null);
                foreach (var projectile in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
                {
                    if (projectile.name != "LargeLogTest(Clone)") { continue; }
                    Assert.AreEqual(2.56f, projectile.transform.localScale.x, .001f);
                    Assert.AreEqual(.768f, (float)typeof(Projectile).GetField("hitRadius", Flags).GetValue(projectile), .001f);
                }
            }
            finally
            {
                foreach (var projectile in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
                { if (projectile.name == "LargeLogTest(Clone)") { Object.DestroyImmediate(projectile.gameObject); } }
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void FireLog_AppliesDirectBurnKeepsStatusesAndPooledVisualRestores()
        {
            var go = new GameObject("FireLogTest"); go.AddComponent<Projectile>();
            var sprite = go.AddComponent<SpriteRenderer>(); var brown = new Color(.55f, .3f, .12f); sprite.color = brown;
            var baseArt = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * .5f);
            var fireArt = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * .5f);
            Assert.IsNotNull(baseArt); Assert.IsNotNull(fireArt); sprite.sprite = baseArt;
            typeof(Projectile).GetField("formSprites", Flags).SetValue(go.GetComponent<Projectile>(),
                new[] { new ProjectileVisual.FormSprite { form = WeaponForm.FireLog, sprite = fireArt } });
            try
            {
                var data = new DefaultWeaponDataProvider(new GameDataStore()).LoadAll().Find(w => w.id == 5);
                var provider = new Provider(); provider.Target.Position = Vector2.up * .1f;
                var weapon = Casters.Projectile(data, go, go.transform, provider);
                foreach (var id in new[] { "log_damage", "log_size", "log_impact", "log_weight", "log_wound", "log_fire" })
                { Assert.IsTrue(weapon.LevelUp(data.upgrades.Find(c => c.id == id))); }
                Assert.IsFalse(weapon.LevelUp(data.upgrades.Find(c => c.id == "log_fire")));
                Assert.AreEqual(WeaponForm.FireLog, Stats(weapon).Cast.Form);
                Assert.AreEqual(16, Stats(weapon).Cast.Damage, .001f);
                typeof(SkillCaster).GetMethod("OnFire", Flags).Invoke(weapon, null);
                Projectile clone = null;
                foreach (var projectile in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
                { if (projectile.name == "FireLogTest(Clone)") { clone = projectile; } }
                Assert.AreEqual(brown, clone.GetComponent<SpriteRenderer>().color);
                Assert.AreSame(fireArt, clone.GetComponent<SpriteRenderer>().sprite);
                Assert.AreEqual(1.6f, clone.transform.localScale.x, .001f);
                typeof(Projectile).GetField("randomValue", Flags).SetValue(clone, (System.Func<float>)(() => 0));
                typeof(Projectile).GetMethod("Tick", Flags).Invoke(clone, new object[] { .01f });
                Assert.AreEqual(1, provider.Target.Burns); Assert.AreEqual(6, provider.Target.BurnDuration);
                Assert.AreEqual(1.6f, provider.Target.BurnDamage, .001f);
                Assert.AreEqual(1, provider.Target.Stuns); Assert.AreEqual(6, provider.Target.SlowDuration); Assert.AreEqual(1, provider.Target.Wounds);
                var reusePool = (UnityEngine.Pool.IObjectPool<Projectile>)typeof(Projectile).GetField("pool", Flags).GetValue(clone);
                reusePool.Release(clone);
                typeof(WeaponBase).GetField("stats", Flags).SetValue(weapon, WeaponStats.FromDefinition(data.baseStats));
                typeof(SkillCaster).GetMethod("OnFire", Flags).Invoke(weapon, null);
                Assert.AreEqual(brown, clone.GetComponent<SpriteRenderer>().color); Assert.AreEqual(Vector3.one, clone.transform.localScale);
                Assert.AreSame(baseArt, clone.GetComponent<SpriteRenderer>().sprite);
                typeof(Projectile).GetMethod("Tick", Flags).Invoke(clone, new object[] { .01f });
                Assert.AreEqual(1, provider.Target.Burns); Assert.AreEqual(1, provider.Target.Stuns); Assert.AreEqual(1, provider.Target.Wounds);
            }
            finally
            {
                foreach (var projectile in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
                { if (projectile.name == "FireLogTest(Clone)") { Object.DestroyImmediate(projectile.gameObject); } }
                Object.DestroyImmediate(go); Object.DestroyImmediate(baseArt); Object.DestroyImmediate(fireArt);
            }
        }

        [Test]
        public void RollingLog_SpawnsInTargetsLaneMovesVerticallyAndPiercesMultipleEnemies()
        {
            var go = new GameObject("RollingLaneTest"); go.AddComponent<Projectile>();
            go.transform.position = new Vector3(-5, -2, 0);
            try
            {
                var data = new DefaultWeaponDataProvider(new GameDataStore()).LoadAll().Find(w => w.id == 5);
                var provider = new LaneProvider();
                var first = new Target { Position = new Vector2(3, 0) };
                var second = new Target { Position = new Vector2(3, 1) };
                var offLane = new Target { Position = new Vector2(2, .5f) };
                provider.Targets.AddRange(new IEnemyTarget[] { first, second, offLane });
                var weapon = Casters.Projectile(data, go, go.transform, provider);
                typeof(SkillCaster).GetMethod("OnFire", Flags).Invoke(weapon, null);
                Projectile clone = null;
                foreach (var projectile in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
                { if (projectile.name == "RollingLaneTest(Clone)") { clone = projectile; } }
                Assert.AreEqual(new Vector3(3, -2, 0), clone.transform.position);
                Assert.AreEqual(Vector3.up, (Vector3)typeof(Projectile).GetField("direction", Flags).GetValue(clone));
                Assert.AreEqual(4, (float)typeof(Projectile).GetField("speed", Flags).GetValue(clone));
                Assert.Greater((float)typeof(Projectile).GetField("lifetime", Flags).GetValue(clone), 5.5f);
                typeof(Projectile).GetMethod("Tick", Flags).Invoke(clone, new object[] { 1f });
                Assert.AreEqual(new Vector3(3, 2, 0), clone.transform.position);
                Assert.AreEqual(1, first.Hits); Assert.AreEqual(1, second.Hits); Assert.AreEqual(0, offLane.Hits);
                Assert.IsTrue(clone.gameObject.activeSelf);
                typeof(Projectile).GetMethod("Tick", Flags).Invoke(clone, new object[] { 1f });
                Assert.AreEqual(1, first.Hits); Assert.AreEqual(1, second.Hits);
            }
            finally
            {
                foreach (var projectile in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
                { if (projectile.name == "RollingLaneTest(Clone)") { Object.DestroyImmediate(projectile.gameObject); } }
                Object.DestroyImmediate(go);
            }
        }

        private class LaneProvider : IEnemyTargetProvider
        {
            public readonly List<IEnemyTarget> Targets = new();
            public int GetNearest(Vector2 from, int count, List<IEnemyTarget> results)
            { results.Clear(); results.AddRange(Targets); return results.Count; }
        }

        [Test]
        public void ActualPlayerPrefab_HasIdFiveProjectileForLogAcquisition()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Stage/Player_Animated.prefab");
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
