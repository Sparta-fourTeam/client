using System.Collections.Generic;
using System.Reflection;
using Game.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Pool;

namespace Game.Tests
{
    public sealed class IceProjectileTests
    {
        private static ProjectileSpawnRules SpawnRules(SkillCaster weapon)
        {
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            return new ProjectileSpawnRules(
                (SkillStats)typeof(SkillBase).GetField("stats", flags).GetValue(weapon),
                (IEnemyTargetProvider)typeof(SkillBase).GetField("targetProvider", flags).GetValue(weapon),
                (ObjectPool<Projectile>)Casters.Projectile(weapon).Pool,
                (Vector3)Casters.Projectile(weapon).Scale);
        }

        private sealed class Target : IEnemyTarget, IFreezableTarget, IKnockbackTarget, IFrostbiteTarget, IParalyzableTarget, IBurnableTarget
        {
            public Vector2 Position { get; set; }
            public int Paralyses;
            public void ApplyParalysis(float duration) { Paralyses++; }
            public int Burns;
            public float BurnDamage;
            public void ApplyBurn(float damage, float duration, float maxHpRatio = 0, System.Action<Vector2> onDeath = null) { Burns++; BurnDamage = damage; }
            public int Hits;
            public int DamageTaken;
            public int Freezes;
            public int Frostbites;
            public float FrostDamage;
            public void ApplyFrostbite(float damage) { Frostbites++; FrostDamage = damage; }
            public int Pushes;
            public float PushDistance;
            public void ApplyKnockback(Vector2 direction, float distance) { Pushes++; PushDistance = distance; }
            public float Duration;
            public void TakeDamage(int damage) { Hits++; DamageTaken += damage; }
            public void ApplyFreeze(float duration) { Freezes++; Duration = duration; }
        }

        private sealed class Provider : IEnemyTargetProvider
        {
            public Target Target = new();
            public Target Second;
            public int GetNearest(Vector2 from, int count, List<IEnemyTarget> results)
            {
                results.Clear();
                results.Add(Target);
                if (Second != null)
                {
                    results.Add(Second);
                }

                return results.Count;
            }
        }

        [Test]
        public void ProjectileHit_AppliesFreezeAndClearsItOnPoolReuse()
        {
            var go = new GameObject("IceProjectileTest");
            try
            {
                var projectile = go.AddComponent<Projectile>();
                var targets = new Provider();
                var pool = new ObjectPool<Projectile>(() => projectile);
                var update = typeof(Projectile).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance);
                pool.Get().Init(pool, Vector3.zero, Vector3.up, 10, 0, 3, targets, freezeDuration: 2, knockbackDistance: 0.6f, frostbiteRatio: 0.1f, burnDuration: 6, burnDamage: 1);
                update.Invoke(projectile, null);
                Assert.AreEqual(1, targets.Target.Hits);
                Assert.AreEqual(1, targets.Target.Freezes);
                Assert.AreEqual(2, targets.Target.Duration);
                Assert.AreEqual(1, targets.Target.Pushes);
                Assert.AreEqual(0.6f, targets.Target.PushDistance);
                Assert.AreEqual(1, targets.Target.Frostbites);
                Assert.AreEqual(1, targets.Target.FrostDamage);
                Assert.AreEqual(1, targets.Target.Burns);
                Assert.AreEqual(1, targets.Target.BurnDamage);
                pool.Get().Init(pool, Vector3.zero, Vector3.up, 10, 0, 3, targets);
                update.Invoke(projectile, null);
                Assert.AreEqual(2, targets.Target.Hits);
                Assert.AreEqual(1, targets.Target.Burns, "재사용 시 점화가 남으면 안 된다");
                Assert.AreEqual(1, targets.Target.Frostbites, "재사용 시 동상이 남으면 안 된다");
                Assert.AreEqual(1, targets.Target.Pushes, "재사용 시 밀침이 남으면 안 된다");
                Assert.AreEqual(1, targets.Target.Freezes, "재사용한 일반 투사체에 빙결이 남으면 안 된다");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void FastProjectile_HitsTargetBetweenFrames()
        {
            var go = new GameObject("FastIceProjectileTest");
            try
            {
                var projectile = go.AddComponent<Projectile>();
                var targets = new Provider();
                targets.Target.Position = new Vector2(5, 0);
                var pool = new ObjectPool<Projectile>(() => projectile);
                pool.Get().Init(pool, Vector3.zero, Vector3.right, 10, 100, 3, targets, freezeDuration: 2);
                typeof(Projectile).GetMethod("Tick", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(projectile, new object[] { 0.1f });
                Assert.AreEqual(1, targets.Target.Hits);
                Assert.AreEqual(1, targets.Target.Freezes);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void FastProjectile_DoesNotHitTargetOutsideTravelRadius()
        {
            var go = new GameObject("FastIceProjectileMissTest");
            try
            {
                var projectile = go.AddComponent<Projectile>();
                var targets = new Provider();
                targets.Target.Position = new Vector2(5, 1);
                var pool = new ObjectPool<Projectile>(() => projectile);
                pool.Get().Init(pool, Vector3.zero, Vector3.right, 10, 100, 3, targets);
                typeof(Projectile).GetMethod("Tick", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(projectile, new object[] { 0.1f });
                Assert.AreEqual(0, targets.Target.Hits);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void TriangleVisual_ReturnsToOriginalSpriteWhenProjectileIsReused()
        {
            var go = new GameObject("TriangleVisualTest");
            var sprite = go.AddComponent<SpriteRenderer>();
            var projectile = go.AddComponent<Projectile>();
            try
            {
                var baseSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * .5f);
                var triangle = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * .5f);
                Assert.IsNotNull(baseSprite); Assert.IsNotNull(triangle);
                sprite.sprite = baseSprite;
                typeof(Projectile).GetField("formSprites", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(projectile,
                    new[] { new ProjectileVisual.FormSprite { form = SkillForm.TriangleIce, sprite = triangle } });
                projectile.SetVisualForm(SkillForm.TriangleIce);
                Assert.AreSame(triangle, sprite.sprite); Assert.IsTrue(sprite.enabled);
                Assert.IsNull(go.GetComponent<LineRenderer>());
                projectile.SetVisualForm(SkillForm.Default);
                Assert.AreSame(baseSprite, sprite.sprite);
                projectile.SetVisualForm(SkillForm.Enbakutsu);
                Assert.AreSame(baseSprite, sprite.sprite);
                Object.DestroyImmediate(baseSprite); Object.DestroyImmediate(triangle);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void LightningRod_TriggersPerMainProjectileAndClearsOnReuse()
        {
            var go = new GameObject("LightningRodTest");
            try
            {
                var projectile = go.AddComponent<Projectile>();
                var provider = new Provider();
                var pool = new ObjectPool<Projectile>(() => projectile);
                var tick = typeof(Projectile).GetMethod("Tick", BindingFlags.NonPublic | BindingFlags.Instance);
                for (int i = 0; i < 3; i++)
                {
                    pool.Get().Init(pool, Vector3.zero, Vector3.up, 10, 0, 3, provider, lightningDamage: 5);
                    tick.Invoke(projectile, new object[] { 0.1f });
                }
                Assert.AreEqual(45, provider.Target.DamageTaken);
                pool.Get().Init(pool, Vector3.zero, Vector3.up, 10, 0, 3, provider);
                tick.Invoke(projectile, new object[] { 0.1f });
                Assert.AreEqual(55, provider.Target.DamageTaken);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void ParalysisChance_ChecksEachHitAndClearsOnReuse()
        {
            var go = new GameObject("ParalysisChanceTest");
            try
            {
                var projectile = go.AddComponent<Projectile>();
                var provider = new Provider();
                var pool = new ObjectPool<Projectile>(() => projectile);
                var tick = typeof(Projectile).GetMethod("Tick", BindingFlags.NonPublic | BindingFlags.Instance);
                pool.Get().Init(pool, Vector3.zero, Vector3.up, 10, 0, 3, provider, paralysisDuration: 1, paralysisChance: 0.2f, randomValue: () => 0.19f);
                tick.Invoke(projectile, new object[] { 0.1f });
                Assert.AreEqual(1, provider.Target.Paralyses);
                pool.Get().Init(pool, Vector3.zero, Vector3.up, 10, 0, 3, provider, paralysisDuration: 1, paralysisChance: 0.2f, randomValue: () => 0.2f);
                tick.Invoke(projectile, new object[] { 0.1f });
                pool.Get().Init(pool, Vector3.zero, Vector3.up, 10, 0, 3, provider);
                tick.Invoke(projectile, new object[] { 0.1f });
                Assert.AreEqual(1, provider.Target.Paralyses);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [TestCase(0f)]
        [TestCase(1f)]
        public void StatusChances_ControlAllFourStatuses(float chance)
        {
            var go = new GameObject("StatusChanceTest");
            try
            {
                var projectile = go.AddComponent<Projectile>();
                var provider = new Provider();
                var pool = new ObjectPool<Projectile>(() => projectile);
                pool.Get().Init(pool, Vector3.zero, Vector3.up, 10, 0, 3, provider, freezeDuration: 2,
                    frostbiteRatio: 0.1f, paralysisDuration: 1, paralysisChance: chance, burnDuration: 6, burnDamage: 1,
                    freezeChance: chance, frostbiteChance: chance, burnChance: chance);
                typeof(Projectile).GetMethod("Tick", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(projectile, new object[] { 0.1f });
                int expected = chance == 1 ? 1 : 0;
                Assert.AreEqual(expected, provider.Target.Freezes);
                Assert.AreEqual(expected, provider.Target.Frostbites);
                Assert.AreEqual(expected, provider.Target.Paralyses);
                Assert.AreEqual(expected, provider.Target.Burns);
                Assert.AreEqual(10, provider.Target.DamageTaken);
                Assert.IsFalse(StatusProc.Roll(float.NaN));
                Assert.IsTrue(StatusProc.Roll(0.2f, () => 0.19f));
                Assert.IsFalse(StatusProc.Roll(0.2f, () => 0.2f));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void FireballExplosion_BurnsOnlyDirectTarget()
        {
            var prefab = new GameObject("ExplosionBurnTest");
            prefab.AddComponent<Projectile>();
            try
            {
                var provider = new Provider { Second = new Target { Position = Vector2.right * 0.5f } };
                var data = new SkillData { baseStats = new SkillBaseStats { cast = { baseDamage = 10, range = 10 }, status = { burnChance = 1 }, explosion = { damageRatio = 1, radius = 0.8f } }, maxLevel = 14 };
                var weapon = Casters.Projectile(data, prefab, prefab.transform, provider);
                SkillStats stats = SkillTestFactory.Apply(SkillTestFactory.Apply(SkillStats.FromDefinition(data.baseStats), "burnDuration", 6), "burnRatio", 10);
                typeof(SkillBase).GetField("stats", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(weapon, stats);
                typeof(SkillCaster).GetMethod("OnFire", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(weapon, null);
                foreach (var projectile in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
                {
                    if (projectile.name == "ExplosionBurnTest(Clone)")
                    {
                        typeof(Projectile).GetMethod("Tick", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(projectile, new object[] { 0.1f });
                    }
                }

                Assert.AreEqual(1, provider.Target.Burns);
                Assert.AreEqual(1f, provider.Target.BurnDamage, 0.001f);
                Assert.AreEqual(0, provider.Second.Burns);
                Assert.AreEqual(20, provider.Target.DamageTaken);
                Assert.AreEqual(10, provider.Second.DamageTaken);
            }
            finally
            {
                foreach (var projectile in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
                {
                    if (projectile.name == "ExplosionBurnTest(Clone)")
                    {
                        Object.DestroyImmediate(projectile.gameObject);
                    }
                }

                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void Catalog_DefaultStatusChancesAreCertain()
        {
            foreach (var weapon in new DefaultSkillDataProvider(new GameDataStore()).LoadAll())
            {
                Assert.AreEqual(1, weapon.baseStats.status.freezeChance);
                Assert.AreEqual(1, weapon.baseStats.status.frostbiteChance);
                Assert.AreEqual(1, weapon.baseStats.status.burnChance);
                Assert.AreEqual(1, weapon.baseStats.status.paralysisChance);
            }
        }

        [Test]
        public void FireballFormsAndFlames_ApplyThreeChildrenAndTwoPierces()
        {
            var prefab = new GameObject("EnbakutsuTest");
            prefab.AddComponent<Projectile>();
            try
            {
                var data = new DefaultSkillDataProvider(new GameDataStore()).LoadAll().Find(w => w.id == 2);
                var weapon = Casters.Projectile(data, prefab, prefab.transform, new Provider());
                weapon.UseChildCaster(new NoopChildCaster());
                foreach (var id in new[] { "fireball_impact_damage", "fireball_explosion_damage", "fireball_explosion_radius", "fireball_flames", "fireball_phoenix" })
                {
                    Assert.IsTrue(weapon.LevelUp(data.upgrades.Find(c => c.id == id), 13));
                }

                var stats = (SkillStats)typeof(SkillBase).GetField("stats", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(weapon);
                Assert.IsTrue(weapon.Config.Children.ContainsKey(14), "불꽃 조각(자식 스킬 14)을 시전하는 연결");
                Assert.AreEqual(2, stats.Projectile.PierceCount);
                Assert.AreEqual(SkillForm.Enbakutsu, stats.Cast.Form);
                typeof(SkillCaster).GetMethod("OnFire", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(weapon, null);
                foreach (var projectile in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
                {
                    if (projectile.name == "EnbakutsuTest(Clone)")
                    {
                        Assert.AreEqual(1.5f, projectile.transform.localScale.x);
                        Assert.AreEqual(0.65f, projectile.transform.localScale.y);
                    }
                }
            }
            finally
            {
                foreach (var projectile in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
                {
                    if (projectile.name == "EnbakutsuTest(Clone)")
                    {
                        Object.DestroyImmediate(projectile.gameObject);
                    }
                }

                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void IcePrefab_IsConnectedToAssetTableAndHasProjectileComponent()
        {
            var projectile = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Skills/IceSpear.prefab");
            Assert.IsNotNull(projectile.GetComponent<Projectile>());
            Assert.AreEqual(projectile, TestSkillAssets.Real().GetPrefab(TestSkillAssets.Data(4).assetKey));
        }
    }
}
