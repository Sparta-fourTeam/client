using System.Collections.Generic;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class SkillTargetSelectorTests
    {
        private sealed class Target : IEnemyTarget
        {
            public Vector2 Position { get; set; }
            public void TakeDamage(int damage) { }
        }

        private sealed class Dead : IEnemyTarget
        {
            public Vector2 Position { get; set; }
            public bool IsDead => true;
            public void TakeDamage(int damage) { }
        }

        private sealed class Targets : IEnemyTargetProvider
        {
            public readonly List<IEnemyTarget> Values = new();
            public int GetNearest(Vector2 from, int count, List<IEnemyTarget> results)
            {
                results.Clear();
                results.AddRange(Values);
                return results.Count;
            }
        }

        [Test]
        public void TargetSelection_FiltersRangeAndKeepsWallPriority()
        {
            var provider = new Targets();
            var nearWall = new Target { Position = new Vector2(2, 1) };
            var nearCaster = new Target { Position = new Vector2(0, 2) };
            provider.Values.AddRange(new IEnemyTarget[] { nearCaster, new Target { Position = new Vector2(100, 0) }, nearWall });
            var selector = new SkillTargetSelector(provider);
            var result = selector.Select(Vector2.zero, 3);
            CollectionAssert.AreEqual(new IEnemyTarget[] { nearWall, nearCaster }, result);
            provider.Values.Clear();
            Assert.IsEmpty(selector.Select(Vector2.zero, 3));
        }

        [Test(Description = "굴러가는 공격은 벽 앞선에서 위로 높이(y)만 재고 가로 위치(x)는 보지 않는다. 벽에 가까운 순이다")]
        public void SelectLane_UsesOnlyTheHeightAboveTheWallLineAndSortsByProximity()
        {
            var provider = new Targets();
            var near = new Target { Position = new Vector2(-40, 1) };         // 가로로 아주 멀어도 높이가 닿으면 고른다
            var far = new Target { Position = new Vector2(30, 5.5f) };
            var tooHigh = new Target { Position = new Vector2(0, 6.5f) };      // 벽 앞선(0)에서 6 이내가 아니다
            provider.Values.AddRange(new IEnemyTarget[] { far, tooHigh, near });
            var selector = new SkillTargetSelector(provider);

            var result = selector.SelectLane(0, 6);

            CollectionAssert.AreEqual(new IEnemyTarget[] { near, far }, result);
        }

        [Test(Description = "굴러가는 공격의 후보는 죽은 적을 빼고 벽에 가까운 8마리까지다")]
        public void SelectLane_SkipsDeadAndKeepsTheEightClosestToTheWall()
        {
            var provider = new Targets();
            var dead = new Dead { Position = new Vector2(0, .1f) };
            provider.Values.Add(dead);
            for (int i = 0; i < 10; i++) { provider.Values.Add(new Target { Position = new Vector2(i, 1 + i * .1f) }); }
            var selector = new SkillTargetSelector(provider);

            var result = selector.SelectLane(0, 6);

            Assert.AreEqual(8, result.Count);
            CollectionAssert.DoesNotContain(result, dead);
            Assert.AreEqual(1f, result[0].Position.y, .0001f);
        }
    }
}
