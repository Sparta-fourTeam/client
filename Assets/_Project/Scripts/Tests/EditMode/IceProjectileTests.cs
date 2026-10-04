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
        private sealed class Target : IEnemyTarget, IFreezableTarget, IKnockbackTarget, IFrostbiteTarget
        {
            public Vector2 Position { get; set; }
            public int Hits;
            public int Freezes;
            public int Frostbites;
            public float FrostDamage;
            public void ApplyFrostbite(float damage) { Frostbites++; FrostDamage = damage; }
            public int Pushes;
            public float PushDistance;
            public void ApplyKnockback(Vector2 direction, float distance) { Pushes++; PushDistance = distance; }
            public float Duration;
            public void TakeDamage(int damage) { Hits++; }
            public void ApplyFreeze(float duration) { Freezes++; Duration = duration; }
        }

        private sealed class Provider : IEnemyTargetProvider
        {
            public Target Target = new();
            public int GetNearest(Vector2 from, int count, List<IEnemyTarget> results)
            {
                results.Clear();
                results.Add(Target);
                return 1;
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
                pool.Get().Init(pool, Vector3.zero, Vector3.up, 10, 0, 3, targets, freezeDuration: 2, knockbackDistance: 0.6f, frostbiteRatio: 0.1f);
                update.Invoke(projectile, null);
                Assert.AreEqual(1, targets.Target.Hits);
                Assert.AreEqual(1, targets.Target.Freezes);
                Assert.AreEqual(2, targets.Target.Duration);
                Assert.AreEqual(1, targets.Target.Pushes);
                Assert.AreEqual(0.6f, targets.Target.PushDistance);
                Assert.AreEqual(1, targets.Target.Frostbites);
                Assert.AreEqual(1, targets.Target.FrostDamage);
                pool.Get().Init(pool, Vector3.zero, Vector3.up, 10, 0, 3, targets);
                update.Invoke(projectile, null);
                Assert.AreEqual(2, targets.Target.Hits);
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
                IWeaponStats stats = new ShardDamageUpgrade(new SplitCountUpgrade(new BaseWeaponStats(data.baseStats), expectedCount), 80);
                typeof(WeaponBase).GetField("stats", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(weapon, stats);
                var callback = (System.Action<Vector2, Vector3, IEnemyTarget>)typeof(ProjectileCaster)
                    .GetMethod("CreateSplitCallback", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(weapon, null);
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
                    Assert.AreEqual(90f, typeof(Projectile).GetField("damage", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(projectile));
                    Assert.IsNull(typeof(Projectile).GetField("onHit", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(projectile));
                    Assert.AreSame(originTarget, typeof(Projectile).GetField("ignoredTarget", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(projectile));
                    Assert.AreEqual(0.5f, projectile.transform.localScale.x);
                }
                Assert.AreEqual(expectedCount, count);
                Assert.AreEqual(100, stats.Damage, "소형 피해 강화는 본체에 적용하지 않는다");
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
                var stats = (IWeaponStats)typeof(WeaponBase).GetField("stats", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(weapon);
                Assert.AreEqual(6, stats.SplitCount);
                Assert.AreEqual(16, stats.Damage, 0.001f);
                Assert.AreEqual(16, stats.Damage * 0.5f * stats.ShardDamageMultiplier, 0.001f);
                var amplify = data.upgrades.Find(c => c.id == "kunai_amplify");
                Assert.IsTrue(weapon.LevelUp(amplify, 13));
                Assert.IsFalse(weapon.LevelUp(amplify, 13));
                stats = (IWeaponStats)typeof(WeaponBase).GetField("stats", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(weapon);
                Assert.AreEqual(32, stats.Damage, 0.001f);
                Assert.AreEqual(32, stats.Damage * 0.5f * stats.ShardDamageMultiplier, 0.001f);

            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void IcePrefab_IsConnectedToPlayerSlotAndHasProjectileComponent()
        {
            var projectile = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Projectile/Projectile4.prefab");
            Assert.IsNotNull(projectile.GetComponent<Projectile>());
            var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player/Player_Animated.prefab");
            var controller = player.GetComponent<WeaponController>();
            var entries = (List<WeaponPrefabEntry>)typeof(WeaponController)
                .GetField("prefabEntries", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(controller);
            Assert.AreEqual(projectile, entries.Find(e => e.id == 4).prefab);
        }
    }
}
