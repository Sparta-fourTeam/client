using Game.View;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class AspectRatioCameraTests
    {
        private const float Target = 9f / 16f;

        private static void AssertRect(Rect expected, Rect actual)
        {
            Assert.AreEqual(expected.x, actual.x, 0.0001f, "x");
            Assert.AreEqual(expected.y, actual.y, 0.0001f, "y");
            Assert.AreEqual(expected.width, actual.width, 0.0001f, "width");
            Assert.AreEqual(expected.height, actual.height, 0.0001f, "height");
        }

        [Test(Description = "화면이 목표 비율과 같으면 화면 전체를 쓴다")]
        public void SameAspect_UsesFullScreen()
        {
            AssertRect(new Rect(0f, 0f, 1f, 1f), AspectRatioCamera.CalculateViewport(1080f, 1920f, Target));
        }

        [Test(Description = "목표보다 긴 화면은 가로를 다 쓰고 세로를 줄여 위아래에 같은 크기의 빈 영역을 만든다")]
        public void TallerScreen_LeavesEqualBarsAboveAndBelow()
        {
            // 1080x2400은 9:20. 9:16 영역의 높이는 전체의 1920/2400 = 0.8
            var rect = AspectRatioCamera.CalculateViewport(1080f, 2400f, Target);

            AssertRect(new Rect(0f, 0.1f, 1f, 0.8f), rect);
        }

        [Test(Description = "목표보다 넓은 화면은 세로를 다 쓰고 가로를 줄여 좌우에 같은 크기의 빈 영역을 만든다")]
        public void WiderScreen_LeavesEqualBarsOnTheSides()
        {
            // 1536x2048은 3:4. 9:16 영역의 너비는 전체의 (9/16)/(3/4) = 0.75
            var rect = AspectRatioCamera.CalculateViewport(1536f, 2048f, Target);

            AssertRect(new Rect(0.125f, 0f, 0.75f, 1f), rect);
        }

        [Test(Description = "가로 화면(에디터 Game 뷰 같은)에서도 가운데 세로 띠가 된다")]
        public void LandscapeScreen_IsCenteredColumn()
        {
            var rect = AspectRatioCamera.CalculateViewport(1920f, 1080f, Target);

            Assert.AreEqual(0f, rect.y, 0.0001f);
            Assert.AreEqual(1f, rect.height, 0.0001f);
            Assert.AreEqual(0.5f, rect.x + rect.width * 0.5f, 0.0001f, "가운데");
            Assert.AreEqual(Target / (1920f / 1080f), rect.width, 0.0001f);
        }

        [Test(Description = "어떤 비율에서도 계산된 영역의 실제 화면 비율은 목표 비율이다")]
        [TestCase(1080f, 1920f)]
        [TestCase(1080f, 2340f)]
        [TestCase(1080f, 2400f)]
        [TestCase(1080f, 2520f)]
        [TestCase(1536f, 2048f)]
        [TestCase(2560f, 1440f)]
        public void ResultingAreaMatchesTargetAspect(float width, float height)
        {
            var rect = AspectRatioCamera.CalculateViewport(width, height, Target);

            float pixelAspect = rect.width * width / (rect.height * height);
            Assert.AreEqual(Target, pixelAspect, 0.0001f);
        }

        [Test(Description = "영역은 항상 화면 안에 있고 가운데에 놓인다")]
        [TestCase(1080f, 2400f)]
        [TestCase(1536f, 2048f)]
        public void Area_IsInsideScreenAndCentered(float width, float height)
        {
            var rect = AspectRatioCamera.CalculateViewport(width, height, Target);

            Assert.GreaterOrEqual(rect.x, 0f);
            Assert.GreaterOrEqual(rect.y, 0f);
            Assert.LessOrEqual(rect.xMax, 1f + 0.0001f);
            Assert.LessOrEqual(rect.yMax, 1f + 0.0001f);
            Assert.AreEqual(0.5f, rect.center.x, 0.0001f);
            Assert.AreEqual(0.5f, rect.center.y, 0.0001f);
        }

        [Test(Description = "화면 크기나 목표 비율이 0 이하이면 0으로 나누지 않고 화면 전체를 돌려준다")]
        [TestCase(0f, 1920f, Target)]
        [TestCase(1080f, 0f, Target)]
        [TestCase(-1f, 1920f, Target)]
        [TestCase(1080f, 1920f, 0f)]
        public void InvalidInput_ReturnsFullScreen(float width, float height, float aspect)
        {
            AssertRect(new Rect(0f, 0f, 1f, 1f), AspectRatioCamera.CalculateViewport(width, height, aspect));
        }
    }
}
