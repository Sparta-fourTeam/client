using System.Collections.Generic;
using Game.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Pool;

namespace Game.Tests
{
    public sealed class SkillLifecycleTests
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
            SkillCaster weapon = null;
            GameObject active = null, inactive = null;
            try
            {
                var data = new SkillData { baseStats = new SkillBaseStats { cast = { cooldown = 1, baseDamage = 10, projectileCount = 1 } }, maxLevel = 10 };
                if (hitscan)
                {
                    prefab.AddComponent<HitscanEffect>();
                    weapon = Casters.Hitscan(data, prefab, prefab.transform, new Targets());
                    var pool = (ObjectPool<HitscanEffect>)Casters.Hitscan(weapon).Pool;
                    var first = pool.Get(); var second = pool.Get(); active = first.gameObject; inactive = second.gameObject; pool.Release(second);
                }
                else
                {
                    prefab.AddComponent<Projectile>();
                    weapon = Casters.Projectile(data, prefab, prefab.transform, new Targets());
                    var pool = Casters.Projectile(weapon).Pool;
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
