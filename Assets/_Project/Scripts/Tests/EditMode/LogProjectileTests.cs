using System.Collections.Generic;
using System.Reflection;
using Game.Core;
using NUnit.Framework;
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

        [Test]
        public void SizeUpgrade_HitsWiderTargetAndPoolReuseRestoresDefaultHitArea()
        {
            var go = new GameObject("LogSizeTest"); go.AddComponent<Projectile>();
            try
            {
                var data = new DefaultSkillDataProvider(new GameDataStore()).LoadAll().Find(w => w.id == 5);
                var provider = new Provider(); provider.Target.Position = Vector2.up * 2;
                var weapon = Casters.Projectile(data, go, go.transform, provider);
                weapon.LevelUp(data.upgrades.Find(c => c.id == "log_size"));
                typeof(SkillCaster).GetMethod("OnFire", Flags).Invoke(weapon, null);
                Projectile clone = null;
                foreach (var projectile in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
                { if (projectile.name == "LogSizeTest(Clone)") { clone = projectile; } }
                provider.Target.Position = new Vector2(.4f, .2f);
                typeof(Projectile).GetMethod("Tick", Flags).Invoke(clone, new object[] { .1f });
                Assert.AreEqual(1, provider.Target.Hits);
                var pool = (UnityEngine.Pool.IObjectPool<Projectile>)typeof(Projectile).GetField("pool", Flags).GetValue(clone);
                pool.Release(clone);
                var reused = pool.Get(); Assert.AreSame(clone, reused);
                reused.Init(pool, Vector3.zero, Vector3.up, 10, 20, 3, provider);
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
                var data = new DefaultSkillDataProvider(new GameDataStore()).LoadAll().Find(w => w.id == 5);
                var provider = new Provider(); provider.Target.Position = Vector2.up;
                var weapon = Casters.Projectile(data, go, go.transform, provider);
                foreach (var id in new[] { "log_impact", "log_weight", "log_wound" })
                { Assert.IsTrue(weapon.LevelUp(data.upgrades.Find(c => c.id == id))); Assert.IsFalse(weapon.LevelUp(data.upgrades.Find(c => c.id == id))); }
                typeof(SkillCaster).GetMethod("OnFire", Flags).Invoke(weapon, null);
                Projectile clone = null;
                foreach (var projectile in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
                { if (projectile.name == "LogStatusCasterTest(Clone)") { clone = projectile; } }
                var hit = HitRecorder.Hit(clone, .05f);
                Assert.AreEqual(.4375f, hit.KnockbackDistance, .001f);
                Assert.AreEqual(1, hit.Stun);
                Assert.AreEqual(0, HitRecorder.Hit(clone, .5f).Stun, "기절 확률은 10%이므로 0.5에서는 걸리지 않는다");
                Assert.AreEqual(5.2f, hit.SlowDuration, .001f);
                Assert.AreEqual(.2f, hit.VulnerabilityRatio, .001f);
                Assert.AreEqual(6, hit.VulnerabilityDuration);
            }
            finally
            {
                foreach (var projectile in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
                { if (projectile.name == "LogStatusCasterTest(Clone)") { Object.DestroyImmediate(projectile.gameObject); } }
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
                new[] { new ProjectileVisual.FormSprite { form = SkillForm.FireLog, sprite = fireArt } });
            try
            {
                var data = new DefaultSkillDataProvider(new GameDataStore()).LoadAll().Find(w => w.id == 5);
                var provider = new Provider(); provider.Target.Position = Vector2.up * .1f;
                var weapon = Casters.Projectile(data, go, go.transform, provider);
                foreach (var id in new[] { "log_damage", "log_size", "log_impact", "log_weight", "log_wound", "log_fire" })
                { Assert.IsTrue(weapon.LevelUp(data.upgrades.Find(c => c.id == id))); }
                Assert.IsFalse(weapon.LevelUp(data.upgrades.Find(c => c.id == "log_fire")));
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
                Assert.AreEqual(2.88f, provider.Target.BurnDamage, .001f);
                Assert.AreEqual(1, provider.Target.Stuns); Assert.AreEqual(5.2f, provider.Target.SlowDuration, .001f); Assert.AreEqual(1, provider.Target.Wounds);
                var reusePool = (UnityEngine.Pool.IObjectPool<Projectile>)typeof(Projectile).GetField("pool", Flags).GetValue(clone);
                reusePool.Release(clone);
                typeof(SkillBase).GetField("stats", Flags).SetValue(weapon, SkillStats.FromDefinition(data.baseStats));
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
            go.transform.position = new Vector3(-1, -2, 0);   // 벽 없이 시전 위치 기준 사정거리(7) 안에 표적이 들어오는 자리
            try
            {
                var data = new DefaultSkillDataProvider(new GameDataStore()).LoadAll().Find(w => w.id == 5);
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
                Assert.AreEqual(3.5f, (float)typeof(Projectile).GetField("speed", Flags).GetValue(clone));
                Assert.Greater((float)typeof(Projectile).GetField("lifetime", Flags).GetValue(clone), 2f);
                typeof(Projectile).GetMethod("Tick", Flags).Invoke(clone, new object[] { 1f });
                Assert.AreEqual(new Vector3(3, 1.5f, 0), clone.transform.position);
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
    }
}
