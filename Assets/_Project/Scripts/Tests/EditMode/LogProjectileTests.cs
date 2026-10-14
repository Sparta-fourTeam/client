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
                using var weapon = Casters.Projectile(data, go, go.transform, provider);
                weapon.LevelUp(data.upgrades.Find(c => c.id == "65"));
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
                using var weapon = Casters.Projectile(data, go, go.transform, provider);
                foreach (var id in new[] { "67", "68", "69" })
                { Assert.IsTrue(weapon.LevelUp(data.upgrades.Find(c => c.id == id))); Assert.IsFalse(weapon.LevelUp(data.upgrades.Find(c => c.id == id))); }
                typeof(SkillCaster).GetMethod("OnFire", Flags).Invoke(weapon, null);
                Projectile clone = null;
                foreach (var projectile in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
                { if (projectile.name == "LogStatusCasterTest(Clone)") { clone = projectile; } }
                var hit = HitRecorder.Hit(clone, 0);
                Assert.AreEqual(weapon.Stats.Projectile.KnockbackDistance, hit.KnockbackDistance, .001f);
                Assert.AreEqual(1, hit.Stun);
                Assert.AreEqual(0, HitRecorder.Hit(clone, 1).Stun, "확률 범위 밖에서는 기절하지 않는다");
                Assert.AreEqual(weapon.Stats.Status.SlowDuration, hit.SlowDuration, .001f);
                Assert.AreEqual(weapon.Stats.Status.VulnerabilityRatio, hit.VulnerabilityRatio, .001f);
                Assert.AreEqual(weapon.Stats.Status.VulnerabilityDuration, hit.VulnerabilityDuration);
            }
            finally
            {
                foreach (var projectile in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
                { if (projectile.name == "LogStatusCasterTest(Clone)") { Object.DestroyImmediate(projectile.gameObject); } }
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void FireLog_AppliesBurnAndClearsStatusesOnPoolReuse()
        {
            var go = new GameObject("FireLogTest"); go.AddComponent<Projectile>();
            try
            {
                var data = new DefaultSkillDataProvider(new GameDataStore()).LoadAll().Find(w => w.id == 5);
                var provider = new Provider(); provider.Target.Position = Vector2.up * .1f;
                using var weapon = Casters.Projectile(data, go, go.transform, provider);
                foreach (var id in new[] { "63", "65", "67", "68", "69", "71" })
                { Assert.IsTrue(weapon.LevelUp(data.upgrades.Find(c => c.id == id))); }
                Assert.IsFalse(weapon.LevelUp(data.upgrades.Find(c => c.id == "71")));
                typeof(SkillCaster).GetMethod("OnFire", Flags).Invoke(weapon, null);
                Projectile clone = null;
                foreach (var projectile in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
                { if (projectile.name == "FireLogTest(Clone)") { clone = projectile; } }
                typeof(Projectile).GetField("randomValue", Flags).SetValue(clone, (System.Func<float>)(() => 0));
                typeof(Projectile).GetMethod("Tick", Flags).Invoke(clone, new object[] { .01f });
                Assert.AreEqual(1, provider.Target.Burns); Assert.AreEqual(weapon.Stats.Burn.Duration, provider.Target.BurnDuration);
                Assert.AreEqual(weapon.Stats.Cast.Damage * weapon.Stats.Burn.DamageRatio, provider.Target.BurnDamage, .001f);
                Assert.AreEqual(1, provider.Target.Stuns); Assert.AreEqual(weapon.Stats.Status.SlowDuration, provider.Target.SlowDuration, .001f); Assert.AreEqual(1, provider.Target.Wounds);
                var reusePool = (UnityEngine.Pool.IObjectPool<Projectile>)typeof(Projectile).GetField("pool", Flags).GetValue(clone);
                reusePool.Release(clone);
                var reused = reusePool.Get();
                reused.Init(reusePool, Vector3.zero, Vector3.up, 10, 0, 3, provider);
                typeof(Projectile).GetMethod("Tick", Flags).Invoke(clone, new object[] { .01f });
                Assert.AreEqual(1, provider.Target.Burns); Assert.AreEqual(1, provider.Target.Stuns); Assert.AreEqual(1, provider.Target.Wounds);
            }
            finally
            {
                foreach (var projectile in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
                { if (projectile.name == "FireLogTest(Clone)") { Object.DestroyImmediate(projectile.gameObject); } }
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Projectile_SortsAllSegmentHitsAndRefreshesDirectionOnPoolReuse()
        {
            var go = new GameObject("ProjectileSegmentOrderTest");
            try
            {
                var projectile = go.AddComponent<Projectile>();
                var provider = new LaneProvider();
                var targets = new IEnemyTarget[5];
                for (int i = 0; i < targets.Length; i++)
                { targets[i] = new Target { Position = Vector2.right * (1 + i * 2) }; }
                provider.Targets.AddRange(new[] { targets[4], targets[1], targets[3], targets[0], targets[2] });
                provider.Targets.Add(new Target { Position = new Vector2(5, 1) });
                var hits = new List<IEnemyTarget>();
                var reactions = new AttackReactions(new[]
                { new ReactionBinding(AttackEvent.Hit, new CastSkillReaction(context => hits.Add(context.Target))) });
                var pool = new UnityEngine.Pool.ObjectPool<Projectile>(() => projectile);
                var tick = typeof(Projectile).GetMethod("Tick", Flags);
                pool.Get().Init(pool, new ProjectileSpawnSettings
                {
                    StartPos = Vector3.zero,
                    Direction = Vector3.right,
                    Speed = 100,
                    Lifetime = 3,
                    PierceCount = 4,
                    TargetProvider = provider,
                    Reactions = reactions
                });
                tick.Invoke(projectile, new object[] { .1f });
                CollectionAssert.AreEqual(targets, hits, "네 번째 후보 이후의 경로 적도 이동 순서대로 맞아야 한다");
                Assert.AreEqual(1, pool.CountInactive);
                hits.Clear();
                Assert.AreSame(projectile, pool.Get());
                projectile.Init(pool, new ProjectileSpawnSettings
                {
                    StartPos = Vector3.right * 10,
                    Direction = Vector3.left,
                    Speed = 100,
                    Lifetime = 3,
                    PierceCount = 4,
                    TargetProvider = provider,
                    Reactions = reactions
                });
                tick.Invoke(projectile, new object[] { .1f });
                CollectionAssert.AreEqual(new[] { targets[4], targets[3], targets[2], targets[1], targets[0] }, hits);
                Assert.AreEqual(1, pool.CountInactive);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void RollingLog_SpawnsInTargetsLaneMovesVerticallyAndPiercesMultipleEnemies()
        {
            var go = new GameObject("RollingLaneTest"); go.AddComponent<Projectile>();
            go.transform.position = new Vector3(-1, -2, 0);   // 벽 없이 시전 위치 기준 사정거리(7) 안에 표적이 들어오는 자리
            try
            {
                var data = new DefaultSkillDataProvider(new GameDataStore()).LoadAll().Find(w => w.id == 5);
                // 충돌 좌표와 진행 거리를 위한 고정 입력. 출시 이동 속도 스냅샷이 아니다.
                data.baseStats.projectile.speed = 3.5f;
                data.baseStats.cast.range = 7;
                data.baseStats.projectile.pierceCount = 2;
                var provider = new LaneProvider();
                var first = new Target { Position = new Vector2(3, 0) };
                var second = new Target { Position = new Vector2(3, 1) };
                var offLane = new Target { Position = new Vector2(2, .5f) };
                provider.Targets.AddRange(new IEnemyTarget[] { first, second, offLane });
                using var weapon = Casters.Projectile(data, go, go.transform, provider);
                typeof(SkillCaster).GetMethod("OnFire", Flags).Invoke(weapon, null);
                Projectile clone = null;
                foreach (var projectile in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
                { if (projectile.name == "RollingLaneTest(Clone)") { clone = projectile; } }
                Assert.AreEqual(new Vector3(3, -2, 0), clone.transform.position);
                Assert.AreEqual(Vector3.up, (Vector3)typeof(Projectile).GetField("direction", Flags).GetValue(clone));
                typeof(Projectile).GetMethod("Tick", Flags).Invoke(clone, new object[] { 1f });
                Assert.AreEqual(new Vector3(3, -2 + weapon.Stats.Projectile.Speed, 0), clone.transform.position);
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
            {
                results.Clear();
                for (int i = 0; i < Targets.Count && i < count; i++) { results.Add(Targets[i]); }
                return results.Count;
            }
        }
    }
}
