using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class ProjectileTests
    {
        private const float HitRadius = 0.3f;

        [Test(Description = "선분 위의 가장 가까운 지점을 0~1 비율로 돌려준다")]
        public void ClosestPointRatio_ReturnsRatioAlongSegment()
        {
            Assert.AreEqual(0.5f, Projectile.ClosestPointRatio(Vector2.zero, new Vector2(0f, 2f), new Vector2(1f, 1f)), 1e-5f);
        }

        [Test(Description = "선분 밖의 점은 양 끝으로 잘린다")]
        public void ClosestPointRatio_ClampsToEnds()
        {
            Assert.AreEqual(0f, Projectile.ClosestPointRatio(Vector2.zero, new Vector2(0f, 2f), new Vector2(0f, -5f)), 1e-5f);
            Assert.AreEqual(1f, Projectile.ClosestPointRatio(Vector2.zero, new Vector2(0f, 2f), new Vector2(0f, 9f)), 1e-5f);
        }

        [Test(Description = "이동 거리가 0이면 0을 돌려준다")]
        public void ClosestPointRatio_ZeroLengthSegment_ReturnsZero()
        {
            Assert.AreEqual(0f, Projectile.ClosestPointRatio(Vector2.one, Vector2.one, new Vector2(3f, 3f)));
        }

        [Test(Description = "낮은 프레임에서 끝점만 보면 놓치는 적도, 지나온 구간으로는 맞는다")]
        public void SweptCheck_HitsEnemyThatEndpointCheckMisses()
        {
            // 30fps에서 속도 20이면 한 프레임에 약 0.67 이동한다
            Vector2 previous = Vector2.zero;
            Vector2 current = new Vector2(0f, 0.67f);
            Vector2 enemy = new Vector2(0.2f, 0.33f);

            Assert.Greater((current - enemy).magnitude, HitRadius, "끝점 기준으로는 빗나간다");

            float ratio = Projectile.ClosestPointRatio(previous, current, enemy);
            Vector2 closest = Vector2.Lerp(previous, current, ratio);
            Assert.LessOrEqual((closest - enemy).magnitude, HitRadius, "지나온 구간 기준으로는 맞는다");
        }
    }
}
