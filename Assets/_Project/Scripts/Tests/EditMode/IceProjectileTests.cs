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
        private static ProjectileSpawnRules SpawnRules(ProjectileCaster weapon)
        {
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            return new ProjectileSpawnRules(
                (WeaponStats)typeof(WeaponBase).GetField("stats", flags).GetValue(weapon), weapon.Data,
                (IEnemyTargetProvider)typeof(WeaponBase).GetField("targetProvider", flags).GetValue(weapon),
                (ObjectPool<Projectile>)typeof(ProjectileCaster).GetField("pool", flags).GetValue(weapon),
                (Vector3)typeof(ProjectileCaster).GetField("projectileScale", flags).GetValue(weapon));
        }

        private static ProjectileBranchSpawner BranchSpawner(ProjectileCaster weapon)
        {
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            return new ProjectileBranchSpawner(weapon.Stats, weapon.Data,
                (IEnemyTargetProvider)typeof(WeaponBase).GetField("targetProvider", flags).GetValue(weapon),
                (ObjectPool<Projectile>)typeof(ProjectileCaster).GetField("pool", flags).GetValue(weapon),
                (Vector3)typeof(ProjectileCaster).GetField("projectileScale", flags).GetValue(weapon));
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

        [TestCase(false)]
        [TestCase(true)]
        public void TriangleSplit_ProducesNormalProjectilesThenOptionalNonRecursiveShards(bool split)
        {
            var prefab = new GameObject("TriangleSplitTest"); prefab.AddComponent<Projectile>();
            var caster = new GameObject("TriangleCasterTest");
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            try
            {
                var data = new WeaponData { baseStats = new WeaponBaseStats { baseDamage = 100, speed = 10, freezeDuration = 2, pierceCount = 2, knockbackDistance = .6f }, maxLevel = 15 };
                var weapon = new ProjectileCaster(data, prefab, caster.transform, new Provider());
                WeaponStats stats = WeaponStatsTestFactory.Apply(WeaponStats.FromDefinition(data.baseStats), UpgradeType.Form, (int)WeaponForm.TriangleIce);
                stats = WeaponStatsTestFactory.Apply(WeaponStatsTestFactory.Apply(stats, UpgradeType.Frostbite, 10), UpgradeType.ShardFrostbite, 10);
                stats = WeaponStatsTestFactory.Apply(stats, UpgradeType.ShardDamage, 80);
                if (split) { stats = WeaponStatsTestFactory.Apply(stats, UpgradeType.SplitCount, 3); }
                typeof(WeaponBase).GetField("stats", flags).SetValue(weapon, stats);
                var callback = (System.Action<Vector2, Vector3, IEnemyTarget>)typeof(ProjectileSpawnRules).GetMethod("CreateHitCallback", flags).Invoke(SpawnRules(weapon), null);
                var source = new Target(); callback(Vector2.zero, Vector3.up, source);
                var normals = new System.Collections.Generic.List<Projectile>();
                foreach (var projectile in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
                { if (projectile.name == "TriangleSplitTest(Clone)") { normals.Add(projectile); } }
                Assert.AreEqual(3, normals.Count);
                foreach (var normal in normals)
                {
                    Assert.AreEqual(50, (float)typeof(ProjectileHitEffects).GetField("damage", flags).GetValue(typeof(Projectile).GetField("hitEffects", flags).GetValue(normal)));
                    Assert.AreEqual(2, (float)typeof(ProjectileHitEffects).GetField("freezeDuration", flags).GetValue(typeof(Projectile).GetField("hitEffects", flags).GetValue(normal)));
                    Assert.AreEqual(.6f, (float)typeof(ProjectileHitEffects).GetField("knockbackDistance", flags).GetValue(typeof(Projectile).GetField("hitEffects", flags).GetValue(normal)));
                    Assert.AreEqual(.1f, (float)typeof(ProjectileHitEffects).GetField("frostbiteRatio", flags).GetValue(typeof(Projectile).GetField("hitEffects", flags).GetValue(normal)), .00001f);
                    Assert.AreSame(source, typeof(Projectile).GetField("ignoredTarget", flags).GetValue(normal));
                    Assert.AreEqual(Vector3.one, normal.transform.localScale);
                }
                var normalHit = (System.Action<Vector2, Vector3, IEnemyTarget>)typeof(Projectile).GetField("onHit", flags).GetValue(normals[0]);
                if (!split) { Assert.IsNull(normalHit); }
                else
                {
                    var secondTarget = new Target(); normalHit(Vector2.up, Vector3.up, secondTarget);
                    int shards = 0;
                    foreach (var projectile in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
                    {
                        if (projectile.name != "TriangleSplitTest(Clone)" || normals.Contains(projectile)) { continue; }
                        shards++;
                        Assert.AreEqual(45, (float)typeof(ProjectileHitEffects).GetField("damage", flags).GetValue(typeof(Projectile).GetField("hitEffects", flags).GetValue(projectile)), .001f);
                        Assert.AreEqual(0, (float)typeof(ProjectileHitEffects).GetField("freezeDuration", flags).GetValue(typeof(Projectile).GetField("hitEffects", flags).GetValue(projectile)));
                        Assert.AreEqual(.1f, (float)typeof(ProjectileHitEffects).GetField("frostbiteRatio", flags).GetValue(typeof(Projectile).GetField("hitEffects", flags).GetValue(projectile)), .00001f);
                        Assert.IsNull(typeof(Projectile).GetField("onHit", flags).GetValue(projectile));
                        Assert.AreSame(secondTarget, typeof(Projectile).GetField("ignoredTarget", flags).GetValue(projectile));
                    }
                    Assert.AreEqual(3, shards);
                }
            }
            finally
            {
                foreach (var projectile in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
                { if (projectile.name == "TriangleSplitTest(Clone)") { Object.DestroyImmediate(projectile.gameObject); } }
                Object.DestroyImmediate(prefab); Object.DestroyImmediate(caster);
            }
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
                    new[] { new ProjectileVisual.FormSprite { form = WeaponForm.TriangleIce, sprite = triangle } });
                projectile.SetVisualForm(WeaponForm.TriangleIce);
                Assert.AreSame(triangle, sprite.sprite); Assert.IsTrue(sprite.enabled);
                Assert.IsNull(go.GetComponent<LineRenderer>());
                projectile.SetVisualForm(WeaponForm.Default);
                Assert.AreSame(baseSprite, sprite.sprite);
                projectile.SetVisualForm(WeaponForm.Enbakutsu);
                Assert.AreSame(baseSprite, sprite.sprite);
                Object.DestroyImmediate(baseSprite); Object.DestroyImmediate(triangle);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [TestCase(3)]
        [TestCase(6)]
        public void SplitCallback_CreatesNonRecursiveShardsWithIndependentDamageBonus(int expectedCount)
        {
            var prefab = new GameObject("ShardTest");
            prefab.AddComponent<Projectile>();
            var caster = new GameObject("ShardCasterTest");
            try
            {
                var data = new WeaponData { baseStats = new WeaponBaseStats { baseDamage = 100, speed = 10 }, maxLevel = 15 };
                var weapon = new ProjectileCaster(data, prefab, caster.transform, new Provider());
                WeaponStats stats = WeaponStatsTestFactory.Apply(WeaponStatsTestFactory.Apply(WeaponStats.FromDefinition(data.baseStats), UpgradeType.SplitCount, expectedCount), UpgradeType.ShardDamage, 80);
                typeof(WeaponBase).GetField("stats", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(weapon, stats);
                var callback = BranchSpawner(weapon).CreateSplitCallback();
                var originTarget = new Target();
                callback(Vector2.zero, Vector3.up, originTarget);
                int count = 0;
                foreach (var projectile in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
                {
                    if (projectile.gameObject == prefab || projectile.name != "ShardTest(Clone)")
                    {
                        continue;
                    }

                    count++;
                    Assert.AreEqual(90f, typeof(ProjectileHitEffects).GetField("damage", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(typeof(Projectile).GetField("hitEffects", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(projectile)));
                    Assert.IsNull(typeof(Projectile).GetField("onHit", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(projectile));
                    Assert.AreSame(originTarget, typeof(Projectile).GetField("ignoredTarget", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(projectile));
                    Assert.AreEqual(0.5f, projectile.transform.localScale.x);
                }
                Assert.AreEqual(expectedCount, count);
                Assert.AreEqual(100, stats.Cast.Damage, "소형 피해 강화는 본체에 적용하지 않는다");
            }
            finally
            {
                foreach (var projectile in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
                {
                    if (projectile.name == "ShardTest(Clone)")
                    {
                        Object.DestroyImmediate(projectile.gameObject);
                    }
                }

                Object.DestroyImmediate(prefab);
                Object.DestroyImmediate(caster);
            }
        }

        [Test]
        public void KunaiAuxiliaryCards_AddToSixAndKeepDamageBonusesSeparate()
        {
            var data = new DefaultWeaponDataProvider().LoadAll().Find(w => w.id == 1);
            var go = new GameObject("KunaiAuxStatsTest");
            try
            {
                var weapon = new ProjectileCaster(data, go, go.transform, new Provider());
                Assert.IsTrue(weapon.LevelUp(data.upgrades.Find(c => c.id == "arrow_sharp"), 13));
                Assert.IsTrue(weapon.LevelUp(data.upgrades.Find(c => c.id == "kunai_spread"), 13));
                Assert.IsTrue(weapon.LevelUp(data.upgrades.Find(c => c.id == "kunai_barrage"), 13));
                Assert.IsTrue(weapon.LevelUp(data.upgrades.Find(c => c.id == "kunai_aux_damage"), 13));
                var stats = (WeaponStats)typeof(WeaponBase).GetField("stats", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(weapon);
                Assert.AreEqual(6, stats.Secondary.Count);
                Assert.AreEqual(16, stats.Cast.Damage, 0.001f);
                Assert.AreEqual(16, stats.Cast.Damage * 0.5f * stats.Secondary.DamageMultiplier, 0.001f);
                var amplify = data.upgrades.Find(c => c.id == "kunai_amplify");
                Assert.IsTrue(weapon.LevelUp(amplify, 13));
                Assert.IsFalse(weapon.LevelUp(amplify, 13));
                stats = (WeaponStats)typeof(WeaponBase).GetField("stats", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(weapon);
                Assert.AreEqual(32, stats.Cast.Damage, 0.001f);
                Assert.AreEqual(32, stats.Cast.Damage * 0.5f * stats.Secondary.DamageMultiplier, 0.001f);

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

        [TestCase(false)]
        [TestCase(true)]
        public void AuxiliaryExplosion_RequiresExplicitUpgradeAndDoesNotSplitAgain(bool enabled)
        {
            var prefab = new GameObject("AuxExplosionTest");
            prefab.AddComponent<Projectile>();
            try
            {
                var provider = new Provider();
                var data = new WeaponData { baseStats = new WeaponBaseStats { baseDamage = 100, explosionDamageRatio = 1, explosionRadius = 0.8f }, maxLevel = 15 };
                var weapon = new ProjectileCaster(data, prefab, prefab.transform, provider);
                WeaponStats stats = WeaponStatsTestFactory.Apply(WeaponStats.FromDefinition(data.baseStats), UpgradeType.SplitCount, 2);
                if (enabled)
                {
                    stats = WeaponStatsTestFactory.Apply(stats, UpgradeType.AuxiliaryExplosion, 1);
                }

                typeof(WeaponBase).GetField("stats", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(weapon, stats);
                var split = BranchSpawner(weapon).CreateSplitCallback();
                split(Vector2.zero, Vector3.up, provider.Target);
                int shards = 0;
                foreach (var projectile in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
                {
                    if (projectile.name != "AuxExplosionTest(Clone)")
                    {
                        continue;
                    }

                    shards++;
                    var callback = (System.Action<Vector2, Vector3, IEnemyTarget>)typeof(Projectile)
                        .GetField("onHit", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(projectile);
                    Assert.AreEqual(enabled, callback != null);
                    callback?.Invoke(Vector2.zero, Vector3.up, provider.Target);
                }
                Assert.AreEqual(2, shards);
                Assert.AreEqual(enabled ? 100 : 0, provider.Target.DamageTaken);
                // 폭발 콜백은 보조 투사체를 다시 생성하지 않는다.
            }
            finally
            {
                foreach (var projectile in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
                {
                    if (projectile.name == "AuxExplosionTest(Clone)")
                    {
                        Object.DestroyImmediate(projectile.gameObject);
                    }
                }

                Object.DestroyImmediate(prefab);
            }
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
                var data = new WeaponData { baseStats = new WeaponBaseStats { baseDamage = 10, explosionDamageRatio = 1, explosionRadius = 0.8f, range = 10, burnChance = 1 }, maxLevel = 14 };
                var weapon = new ProjectileCaster(data, prefab, prefab.transform, provider);
                WeaponStats stats = WeaponStatsTestFactory.Apply(WeaponStatsTestFactory.Apply(WeaponStats.FromDefinition(data.baseStats), UpgradeType.BurnDuration, 6), UpgradeType.BurnRatio, 10);
                typeof(WeaponBase).GetField("stats", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(weapon, stats);
                typeof(ProjectileCaster).GetMethod("OnFire", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(weapon, null);
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
            foreach (var weapon in new DefaultWeaponDataProvider().LoadAll())
            {
                Assert.AreEqual(1, weapon.baseStats.freezeChance);
                Assert.AreEqual(1, weapon.baseStats.frostbiteChance);
                Assert.AreEqual(1, weapon.baseStats.burnChance);
                Assert.AreEqual(1, weapon.baseStats.paralysisChance);
            }
        }

        [Test]
        public void FireballFormsAndFlames_ApplyThreeChildrenAndTwoPierces()
        {
            var prefab = new GameObject("EnbakutsuTest");
            prefab.AddComponent<Projectile>();
            try
            {
                var data = new DefaultWeaponDataProvider().LoadAll().Find(w => w.id == 2);
                var weapon = new ProjectileCaster(data, prefab, prefab.transform, new Provider());
                foreach (var id in new[] { "fireball_impact_damage", "fireball_explosion_damage", "fireball_explosion_radius", "fireball_flames", "fireball_enbakutsu" })
                {
                    Assert.IsTrue(weapon.LevelUp(data.upgrades.Find(c => c.id == id), 13));
                }

                var stats = (WeaponStats)typeof(WeaponBase).GetField("stats", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(weapon);
                Assert.AreEqual(3, stats.Secondary.Count);
                Assert.AreEqual(2, stats.Projectile.PierceCount);
                Assert.AreEqual(WeaponForm.Enbakutsu, stats.Cast.Form);
                typeof(ProjectileCaster).GetMethod("OnFire", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(weapon, null);
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
        public void IcePrefab_IsConnectedToPlayerSlotAndHasProjectileComponent()
        {
            var projectile = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Skills/IceSpear/IceSpear.prefab");
            Assert.IsNotNull(projectile.GetComponent<Projectile>());
            var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player/Player_Animated.prefab");
            var controller = player.GetComponent<WeaponController>();
            var entries = (List<WeaponPrefabEntry>)typeof(WeaponController)
                .GetField("prefabEntries", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(controller);
            Assert.AreEqual(projectile, entries.Find(e => e.id == 4).prefab);
        }
    }
}
