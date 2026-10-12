using System.Collections.Generic;
using Game.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Tests
{
    /// <summary>적 전체를 발끝 높이로 한 줄로 세우는 정렬. 겹친 적끼리 순서가 같아지거나 앞뒤가 떨리면 깜빡여 보인다</summary>
    public sealed class EnemyDepthSorterTests
    {
        private readonly List<GameObject> _enemies = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var enemy in _enemies) { Object.DestroyImmediate(enemy); }
            _enemies.Clear();
            EnemyDepthSorter.Sort(); // 지운 적을 줄에서 뺀다
        }

        // 스프라이트가 없으면 발끝은 위치 높이와 같다
        private EnemyDepthSort Spawn(float y)
        {
            var enemy = new GameObject("Enemy");
            enemy.transform.position = new Vector3(0f, y, 0f);
            new GameObject("Visual", typeof(SortingGroup)).transform.SetParent(enemy.transform, false);
            _enemies.Add(enemy);
            var sort = enemy.AddComponent<EnemyDepthSort>();
            sort.Bind();
            return sort;
        }

        private static void MoveTo(EnemyDepthSort enemy, float y) => enemy.transform.position = new Vector3(0f, y, 0f);

        [Test(Description = "같은 높이에 멈춘 적끼리도 순서가 같지 않고, 프레임이 지나도 앞뒤가 그대로다")]
        public void SameHeight_GetsDistinctStableOrders()
        {
            var first = Spawn(-2f);
            var second = Spawn(-2f);
            EnemyDepthSorter.Sort();
            int firstOrder = first.Order;
            int secondOrder = second.Order;

            for (int i = 0; i < 5; i++) { EnemyDepthSorter.Sort(); }

            Assert.AreNotEqual(firstOrder, secondOrder);
            Assert.AreEqual(firstOrder, first.Order);
            Assert.AreEqual(secondOrder, second.Order);
        }

        [Test(Description = "발끝이 더 낮은 적이 앞에 그려지고, 다른 적을 확실히 지나치면 앞뒤가 바뀐다")]
        public void LowerFeet_DrawInFront()
        {
            var back = Spawn(1f);
            var front = Spawn(0f);
            EnemyDepthSorter.Sort();
            Assert.Greater(front.Order, back.Order);

            MoveTo(back, -0.5f);
            EnemyDepthSorter.Sort();

            Assert.Greater(back.Order, front.Order);
        }

        [Test(Description = "높이 차가 히스테리시스보다 작게 오르내리면 앞뒤를 바꾸지 않는다")]
        public void SmallJitter_KeepsOrder()
        {
            var front = Spawn(0f);
            var back = Spawn(0.01f);
            EnemyDepthSorter.Sort();
            Assert.Greater(front.Order, back.Order);

            MoveTo(back, -EnemyDepthSorter.Hysteresis * 0.5f);
            EnemyDepthSorter.Sort();
            MoveTo(back, 0.01f);
            EnemyDepthSorter.Sort();

            Assert.Greater(front.Order, back.Order);
        }

        [Test(Description = "이웃한 적의 순서는 상태이상(+1)·사망 이펙트(-1)가 끼어들 만큼 떨어져 있다")]
        public void Neighbours_LeaveRoomForEffects()
        {
            var front = Spawn(0f);
            var back = Spawn(1f);
            EnemyDepthSorter.Sort();

            Assert.Greater(front.Order - 1, back.Order + 1);
        }
    }
}
