using UnityEngine;

namespace Game.View
{
    /// <summary>화면 전체를 덮는 튜토리얼 가림판에 붙인다. Target 영역의 터치만 통과시켜 강조한 버튼만 누를 수 있게 한다</summary>
    public sealed class TutorialRaycastHole : MonoBehaviour, ICanvasRaycastFilter
    {
        public RectTransform Target { get; set; }

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera) =>
            Target == null || !RectTransformUtility.RectangleContainsScreenPoint(Target, screenPoint, CameraOf(Target));

        /// <summary>rect가 그려지는 캔버스의 카메라. Overlay 캔버스면 null</summary>
        public static Camera CameraOf(RectTransform rect)
        {
            var canvas = rect.GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                return null;
            }

            canvas = canvas.rootCanvas;
            return canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        }
    }
}
