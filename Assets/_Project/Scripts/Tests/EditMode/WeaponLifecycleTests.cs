using System.Collections.Generic;
using System.Reflection;
using Game.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Pool;

namespace Game.Tests
{
    public sealed class WeaponLifecycleTests
    {
        private sealed class Targets : IEnemyTargetProvider
        {
            public int GetNearest(Vector2 from, int count, List<IEnemyTarget> results) { results.Clear(); return 0; }
        }
        [TestCase(false)]
        [TestCase(true)]
        public void Dispose_DestroysInactiveAndInFlightEffects_AndIsRepeatable(bool hitscan)
        {
            var prefab = new GameObject("PoolLifecyclePrefab");
            WeaponBase weapon = null;
            GameObject active = null, inactive = null;
            try
            {
                var data = new WeaponData { baseStats = new WeaponBaseStats { cast = { cooldown = 1, baseDamage = 10, projectileCount = 1 } }, maxLevel = 10 };
                if (hitscan)
                {
                    prefab.AddComponent<HitscanEffect>();
                    weapon = new HitscanCaster(data, prefab, prefab.transform, new Targets());
                    var pool = (ObjectPool<HitscanEffect>)typeof(HitscanCaster).GetField("pool", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(weapon);
                    var first = pool.Get(); var second = pool.Get(); active = first.gameObject; inactive = second.gameObject; pool.Release(second);
                }
                else
                {
                    prefab.AddComponent<Projectile>();
                    weapon = new ProjectileCaster(data, prefab, prefab.transform, new Targets());
                    var pool = (ObjectPool<Projectile>)typeof(ProjectileCaster).GetField("pool", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(weapon);
                    var first = pool.Get(); var second = pool.Get(); active = first.gameObject; inactive = second.gameObject; pool.Release(second);
                }
                weapon.Dispose(); weapon.Dispose(); weapon.Tick(1);
                Assert.IsTrue(active == null, "In-flight effect must be destroyed at teardown.");
                Assert.IsTrue(inactive == null, "Idle pooled effect must be destroyed at teardown.");
            }
            finally { weapon?.Dispose(); if (active != null) { Object.DestroyImmediate(active); } if (inactive != null) { Object.DestroyImmediate(inactive); } Object.DestroyImmediate(prefab); }
        }
    }
}
