using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Tests
{
    public class LightningOrbTests
    {
        private const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;
        private class Provider : IEnemyTargetProvider
        {
            public int GetNearest(Vector2 from, int count, List<IEnemyTarget> results) { results.Clear(); return 0; }
        }
        private static T Read<T>(object obj, string name)
        {
            var field = obj.GetType().GetField(name, Flags);
            if (field != null) { return (T)field.GetValue(obj); }
            var effects = typeof(Projectile).GetField("hitEffects", Flags).GetValue(obj);
            return (T)typeof(ProjectileHitEffects).GetField(name, Flags).GetValue(effects);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Orbs_SnapshotSixRadialProjectilesWithIndependentParalysis(bool voltage)
        {
            var root = new GameObject("OrbTestCaster");
            var orb = new GameObject("OrbTestProjectile");
            orb.AddComponent<Projectile>();
            var effect = root.AddComponent<HitscanEffect>();
            typeof(HitscanEffect).GetField("secondaryProjectilePrefab", Flags).SetValue(effect, orb);
            try
            {
                var data = new DefaultWeaponDataProvider().LoadAll().Find(w => w.id == 3);
                var weapon = new HitscanCaster(data, root, root.transform, new Provider());
                Assert.IsTrue(weapon.LevelUp(data.upgrades.Find(c => c.id == "lightning_voltage")));
                Assert.IsTrue(weapon.LevelUp(data.upgrades.Find(c => c.id == "lightning_split")));
                Assert.IsFalse(weapon.LevelUp(data.upgrades.Find(c => c.id == "lightning_split")));
                if (voltage) { Assert.IsTrue(weapon.LevelUp(data.upgrades.Find(c => c.id == "lightning_particle_voltage"))); }
                var callback = (Action<Vector2>)typeof(HitscanCaster).GetMethod("CreateOrbCallback", Flags).Invoke(weapon, new object[] { null });
                callback(new Vector2(1000, 1000));
                var clones = new List<Projectile>();
                foreach (var projectile in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
                {
                    if (projectile.name == "OrbTestProjectile(Clone)") { clones.Add(projectile); }
                }
                Assert.AreEqual(6, clones.Count);
                var directions = new List<Vector3>();
                foreach (var projectile in clones)
                {
                    Assert.AreEqual(32.5f * (voltage ? .7f : .5f), Read<float>(projectile, "damage"), .001f);
                    Assert.AreEqual(voltage ? .5f : 0, Read<float>(projectile, "paralysisDuration"));
                    Assert.AreEqual(10, Read<float>(projectile, "speed"));
                    Assert.AreEqual(3, Read<float>(projectile, "lifetime"));
                    Assert.IsNull(Read<object>(projectile, "onHit"));
                    var direction = Read<Vector3>(projectile, "direction");
                    foreach (var previous in directions) { Assert.LessOrEqual(Vector3.Dot(previous, direction), .501f); }
                    directions.Add(direction);
                }
            }
            finally
            {
                foreach (var projectile in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
                { if (projectile.name == "OrbTestProjectile(Clone)") { Object.DestroyImmediate(projectile.gameObject); } }
                Object.DestroyImmediate(root); Object.DestroyImmediate(orb);
            }
        }

        [Test]
        public void ActualLightningPrefab_HasProjectileForElectronSplit()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/VFX/Lightning_Lv1.prefab");
            Assert.IsNotNull(prefab.GetComponent<HitscanEffect>().SecondaryProjectilePrefab.GetComponent<Projectile>());
            var data = new DefaultWeaponDataProvider().LoadAll().Find(w => w.id == 3);
            CollectionAssert.AreEquivalent(new[] { "lightning_voltage", "lightning_damage" }, data.upgrades.Find(c => c.id == "lightning_split").requiredCardIds);
            CollectionAssert.AreEqual(new[] { "lightning_split" }, data.upgrades.Find(c => c.id == "lightning_particle_voltage").requiredCardIds);
        }
    }
}
