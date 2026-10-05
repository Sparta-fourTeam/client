using System.Collections.Generic;
using System.Reflection;
using Game.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

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
            return (T)obj.GetType().GetField(name, Flags).GetValue(obj);
        }

        [Test]
        public void ActualLightningPrefab_HasProjectileForElectronSplit()
        {
            var orb = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Skills/LightningOrb.prefab");
            Assert.IsNotNull(orb.GetComponent<Projectile>(), "전기 구체 자식 스킬(id 15)의 프리팹");
            var data = new DefaultWeaponDataProvider(new GameDataStore()).LoadAll().Find(w => w.id == 3);
            CollectionAssert.AreEquivalent(new[] { "lightning_voltage", "lightning_damage" }, data.upgrades.Find(c => c.id == "lightning_split").requiredCardIds);
            CollectionAssert.AreEqual(new[] { "lightning_split" }, data.upgrades.Find(c => c.id == "lightning_particle_voltage").requiredCardIds);
        }
    }
}
