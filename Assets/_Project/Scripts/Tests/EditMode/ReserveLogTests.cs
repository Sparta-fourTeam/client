using System.Collections.Generic;
using System.Reflection;
using Game.Core;
using Game.Core.Defense;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Tests
{
    public class ReserveLogTests
    {
        private class Target : IEnemyTarget
        {
            public Vector2 Position { get; set; }
            public void TakeDamage(int damage) { }
        }
        private class Provider : IEnemyTargetProvider
        {
            public Target Target = new() { Position = new Vector2(3, .5f) };
            public int GetNearest(Vector2 from, int count, List<IEnemyTarget> results)
            { results.Clear(); results.Add(Target); return 1; }
        }
        [Test]
        public void ReactiveClock_ThreeCastsRespectIntervalPauseAndCooldown()
        {
            var clock = new ReactiveCastClock(); int shots = 0;
            clock.Tick(0, true, 3, 10, .1f, () => shots++); Assert.AreEqual(0, shots);
            clock.Tick(.01f, false, 3, 10, .1f, () => shots++); Assert.AreEqual(0, shots);
            clock.Tick(.01f, true, 3, 10, .1f, () => shots++); Assert.AreEqual(1, shots);
            clock.Tick(.05f, true, 3, 10, .1f, () => shots++); Assert.AreEqual(1, shots);
            clock.Tick(.05f, true, 3, 10, .1f, () => shots++); Assert.AreEqual(2, shots);
            clock.Tick(.1f, true, 3, 10, .1f, () => shots++); Assert.AreEqual(3, shots);
            clock.Tick(9, true, 3, 10, .1f, () => shots++); Assert.AreEqual(3, shots);
            clock.Tick(1, true, 3, 10, .1f, () => shots++); Assert.AreEqual(4, shots);
        }
        [Test]
        public void ReactiveClock_LargeDeltaCompletesOnlyPendingBurst()
        {
            var clock = new ReactiveCastClock(); int shots = 0;
            clock.Tick(.01f, true, 3, 10, .1f, () => shots++);
            clock.Tick(100, true, 3, 10, .1f, () => shots++);
            Assert.AreEqual(3, shots);
        }
        [Test]
        public void ReserveLog_UsesRealWallLineAndMaintainsNormalCastsAndRollingLane()
        {
            var prefab = new GameObject("ReserveLogTest"); prefab.AddComponent<Projectile>();
            var wallGo = new GameObject("ReserveWallTest");
            try
            {
                var wall = wallGo.AddComponent<Wall>();
                wallGo.transform.position = new Vector3(0, -2, 0);
                typeof(Wall).GetField("_attackLineOffset", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(wall, 2f);
                prefab.transform.position = new Vector3(-5, -4, 0);
                var provider = new Provider();
                var data = new DefaultSkillDataProvider(new GameDataStore()).LoadAll().Find(w => w.id == 5);
                var weapon = SkillFactory.Create(data, prefab, prefab.transform, provider, wall);
                Assert.IsTrue(weapon.LevelUp(data.upgrades.Find(c => c.id == "log_reserve")));
                Assert.IsFalse(weapon.LevelUp(data.upgrades.Find(c => c.id == "log_reserve")));
                weapon.Tick(0); Assert.AreEqual(1, CountLogs());
                provider.Target.Position = new Vector2(3, 1.1f);
                weapon.Tick(.01f); Assert.AreEqual(1, CountLogs());
                provider.Target.Position = new Vector2(3, .5f);
                weapon.Tick(.01f); Assert.AreEqual(2, CountLogs());
                weapon.Tick(.1f); weapon.Tick(.1f); Assert.AreEqual(4, CountLogs());
                foreach (var projectile in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
                {
                    if (projectile.name != "ReserveLogTest(Clone)") { continue; }
                    Assert.AreEqual(new Vector3(3, .2f, 0), projectile.transform.position);
                    Assert.AreEqual(Vector3.up, (Vector3)typeof(Projectile).GetField("direction", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(projectile));
                }
                weapon.Tick(.5f); Assert.AreEqual(4, CountLogs());
            }
            finally
            {
                foreach (var projectile in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
                { if (projectile.name == "ReserveLogTest(Clone)") { Object.DestroyImmediate(projectile.gameObject); } }
                Object.DestroyImmediate(prefab); Object.DestroyImmediate(wallGo);
            }
        }
        private static int CountLogs()
        {
            int count = 0;
            foreach (var projectile in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
            { if (projectile.name == "ReserveLogTest(Clone)") { count++; } }
            return count;
        }
    }
}
