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
        private sealed class Target : IEnemyTarget, IFreezableTarget
        {
            public Vector2 Position => Vector2.zero;
            public int Hits;
            public int Freezes;
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
                pool.Get().Init(pool, Vector3.zero, Vector3.up, 10, 0, 3, targets, freezeDuration: 2);
                update.Invoke(projectile, null);
                Assert.AreEqual(1, targets.Target.Hits);
                Assert.AreEqual(1, targets.Target.Freezes);
                Assert.AreEqual(2, targets.Target.Duration);
                pool.Get().Init(pool, Vector3.zero, Vector3.up, 10, 0, 3, targets);
                update.Invoke(projectile, null);
                Assert.AreEqual(2, targets.Target.Hits);
                Assert.AreEqual(1, targets.Target.Freezes, "재사용한 일반 투사체에 빙결이 남으면 안 된다");
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
