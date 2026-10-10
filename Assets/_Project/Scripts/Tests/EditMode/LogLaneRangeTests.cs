using System.Collections.Generic;
using Game.Core;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Tests
{
    /// <summary>나무뿌리(굴러가는 공격)는 플레이어에서 발사되지 않으므로 사정거리를 벽 앞선에서 위로의 높이(y)로만 잰다</summary>
    public sealed class LogLaneRangeTests
    {
        private readonly List<GameObject> created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in created) { if (go != null) { Object.DestroyImmediate(go); } }
            created.Clear();
            foreach (var projectile in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
            { if (projectile.name.Contains("(Clone)")) { Object.DestroyImmediate(projectile.gameObject); } }
        }

        private sealed class Provider : IEnemyTargetProvider
        {
            public readonly List<IEnemyTarget> All = new List<IEnemyTarget>();
            public int GetNearest(Vector2 from, int count, List<IEnemyTarget> results) { results.Clear(); results.AddRange(All); return results.Count; }
        }

        private sealed class Target : IEnemyTarget
        {
            public Vector2 Position { get; set; }
            public void TakeDamage(int amount) { }
        }

        // 나무뿌리의 사정거리(데이터 값)와 테스트의 벽 앞선 높이
        private static float LogRange => new DefaultSkillDataProvider(new GameDataStore()).LoadAll().Find(w => w.id == 5).baseStats.cast.range;
        private const float WallLineY = -2f;

        private int LogsFiredAt(float x, float y)
        {
            var go = new GameObject("LogLaneRange"); go.AddComponent<Projectile>(); created.Add(go);
            var wallObject = new GameObject("LogLaneWall"); wallObject.transform.position = new Vector3(0, -2, 0); created.Add(wallObject);
            var wall = wallObject.AddComponent<Game.Core.Defense.Wall>();
            var data = new DefaultSkillDataProvider(new GameDataStore()).LoadAll().Find(w => w.id == 5);
            var provider = new Provider();
            provider.All.Add(new Target { Position = new Vector2(x, y) });
            var weapon = Casters.Projectile(data, go, go.transform, provider, wall);

            typeof(SkillCaster).GetMethod("OnFire", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(weapon, null);

            int count = 0;
            foreach (var projectile in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None)) { if (projectile.name == "LogLaneRange(Clone)") { count++; } }
            return count;
        }

        [Test(Description = "나무뿌리는 가로 위치와 상관없이 벽 앞선에서 사정거리 높이 안의 적이 있으면 굴러간다")]
        public void Log_FiresWhenAnEnemyIsWithinRangeAboveTheWallLineRegardlessOfX()
        {
            Assert.AreEqual(1, LogsFiredAt(50, WallLineY + LogRange - 1), "가로로 아주 멀어도 벽 앞선에서 사정거리 안쪽 높이다");
        }

        [Test(Description = "벽 앞선에서 사정거리보다 높이 있는 적만 있으면 굴러가지 않는다")]
        public void Log_DoesNotFireWhenEveryEnemyIsHigherThanTheRange()
        {
            Assert.AreEqual(0, LogsFiredAt(0, WallLineY + LogRange + 1), "벽 앞선에서 사정거리보다 위라 사정거리 밖이다");
        }
    }
}
