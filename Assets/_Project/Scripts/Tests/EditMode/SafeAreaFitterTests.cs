using Game.View;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class SafeAreaFitterTests
    {
        private static readonly Rect FullScreen = new Rect(0f, 0f, 1f, 1f);

        private static void AssertAnchors(Vector2 expectedMin, Vector2 expectedMax, Rect safeArea, Rect cameraRect)
        {
            SafeAreaFitter.CalculateAnchors(safeArea, cameraRect, out Vector2 min, out Vector2 max);
            Assert.AreEqual(expectedMin.x, min.x, 0.0001f, "min.x");
            Assert.AreEqual(expectedMin.y, min.y, 0.0001f, "min.y");
            Assert.AreEqual(expectedMax.x, max.x, 0.0001f, "max.x");
            Assert.AreEqual(expectedMax.y, max.y, 0.0001f, "max.y");
        }

        [Test(Description = "카메라가 화면 전체이면 Safe Area가 그대로 앵커가 된다 (이전 동작과 같다)")]
        public void FullCamera_UsesSafeAreaAsIs()
        {
            var safe = new Rect(0.05f, 0.02f, 0.9f, 0.9f);

            AssertAnchors(new Vector2(0.05f, 0.02f), new Vector2(0.95f, 0.92f), safe, FullScreen);
        }

        [Test(Description = "노치와 홈 인디케이터가 위아래 빈 영역에 들어가면 9:16 영역 안쪽 여백은 0이다")]
        public void NotchInsideLetterbox_GivesNoInset()
        {
            // 9:20 화면: 9:16 영역은 y 0.1 ~ 0.9. Safe Area는 y 0.04 ~ 0.96이라 영역을 덮는다
            var camera = new Rect(0f, 0.1f, 1f, 0.8f);
            var safe = new Rect(0f, 0.04f, 1f, 0.92f);

            AssertAnchors(Vector2.zero, Vector2.one, safe, camera);
        }

        [Test(Description = "Safe Area가 9:16 영역 안으로 파고들면 그만큼만 영역 기준으로 환산한 앵커가 된다")]
        public void SafeAreaCuttingIntoCameraArea_IsConvertedToCameraSpace()
        {
            // 영역 y 0.1 ~ 0.9, Safe Area 위쪽이 0.85까지: (0.85 - 0.1) / 0.8
            var camera = new Rect(0f, 0.1f, 1f, 0.8f);
            var safe = new Rect(0f, 0.04f, 1f, 0.81f);

            AssertAnchors(Vector2.zero, new Vector2(1f, 0.9375f), safe, camera);
        }

        [Test(Description = "좌우 빈 영역(태블릿 같은 넓은 화면)도 같은 방식으로 환산한다")]
        public void Pillarbox_IsConvertedToCameraSpace()
        {
            // 영역 x 0.125 ~ 0.875, Safe Area가 왼쪽에서 0.2부터: (0.2 - 0.125) / 0.75
            var camera = new Rect(0.125f, 0f, 0.75f, 1f);
            var safe = new Rect(0.2f, 0f, 0.8f, 1f);

            AssertAnchors(new Vector2(0.1f, 0f), Vector2.one, safe, camera);
        }

        [Test(Description = "Safe Area와 카메라 영역이 겹치지 않으면 전체를 돌려준다 (UI가 사라지지 않게)")]
        public void NoOverlap_ReturnsFullAnchors()
        {
            var camera = new Rect(0f, 0.1f, 1f, 0.2f);
            var safe = new Rect(0f, 0.5f, 1f, 0.4f);

            AssertAnchors(Vector2.zero, Vector2.one, safe, camera);
        }

        [Test(Description = "카메라 영역이 비어 있으면 0으로 나누지 않고 전체를 돌려준다")]
        public void EmptyCameraRect_ReturnsFullAnchors()
        {
            AssertAnchors(Vector2.zero, Vector2.one, FullScreen, new Rect(0f, 0f, 0f, 1f));
        }
    }
}
