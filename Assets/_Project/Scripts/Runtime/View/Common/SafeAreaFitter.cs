using UnityEngine;
using DeviceScreen = UnityEngine.Device.Screen;

namespace Game.View
{
    /// <summary>RectTransform을 기기의 Safe Area(노치·펀치홀·홈 인디케이터를 뺀 영역)에 맞춘다.
    /// 부모는 화면 전체로 늘어나 있어야 한다. 로비 UI 전체를 담는 최상위 컨테이너에 하나만 붙인다.
    /// Unity에는 해상도 변경 이벤트가 없으므로, 부모 캔버스 크기가 바뀔 때 불리는 OnRectTransformDimensionsChange로 다시 맞춘다.
    /// 해상도는 그대로이고 Safe Area만 바뀌는 경우(에디터 Simulator에서 기기만 바꿀 때)는 다시 맞추지 않는다.
    /// Device Simulator에서는 UnityEngine.Screen이 창의 실제 크기를 주고 safeArea만 기기 값을 줘서 서로 안 맞는다.
    /// UnityEngine.Device.Screen은 Simulator에서는 기기 값, 빌드에서는 Screen과 같은 값을 주므로 이쪽을 쓴다.
    /// 캔버스가 카메라에 그려질 때(Screen Space - Camera)는 화면 전체가 아니라 그 카메라가 차지하는 영역(AspectRatioCamera가 만든 9:16 영역)이 캔버스다.
    /// 그래서 Safe Area를 카메라 영역과 겹치는 부분만 잘라 그 영역 기준으로 환산한다. 노치가 빈 영역에 들어가면 안쪽 여백은 0이 된다</summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform _rect;
        private Rect _appliedArea;
        private Rect _appliedCameraRect;
        private Vector2Int _appliedScreen;
        private Canvas _rootCanvas;

        private void Awake()
        {
            _rect = (RectTransform)transform;
            Apply();
        }

        // 화면 크기·회전이 바뀌면 캔버스와 함께 이 오브젝트의 크기가 바뀌어 불린다.
        // Apply가 앵커를 바꿔 다시 불려도 값이 같으면 건너뛴다
        private void OnRectTransformDimensionsChange()
        {
            if (_rect == null)
            {
                return;
            }

            if (DeviceScreen.safeArea != _appliedArea
                || DeviceScreen.width != _appliedScreen.x
                || DeviceScreen.height != _appliedScreen.y
                || CurrentCameraRect() != _appliedCameraRect)
            {
                Apply();
            }
        }

        /// <summary>이 캔버스가 그려지는 카메라의 화면상 영역(0~1). Overlay이거나 카메라가 없으면 화면 전체</summary>
        private Rect CurrentCameraRect()
        {
            if (_rootCanvas == null)
            {
                Canvas canvas = GetComponentInParent<Canvas>();
                _rootCanvas = canvas != null ? canvas.rootCanvas : null;
            }

            if (_rootCanvas != null && _rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay && _rootCanvas.worldCamera != null)
            {
                return _rootCanvas.worldCamera.rect;
            }

            return new Rect(0f, 0f, 1f, 1f);
        }

        /// <summary>Safe Area(0~1, 화면 전체 기준)와 카메라 영역(0~1)이 겹치는 부분을 카메라 영역 기준 앵커로 환산한다.
        /// 카메라가 화면 전체이면 Safe Area 그대로다. 겹치는 부분이 없거나 카메라 영역이 비어 있으면 전체(0,0)~(1,1)를 돌려준다</summary>
        public static void CalculateAnchors(Rect safeAreaNormalized, Rect cameraRect, out Vector2 anchorMin, out Vector2 anchorMax)
        {
            float minX = Mathf.Max(safeAreaNormalized.xMin, cameraRect.xMin);
            float minY = Mathf.Max(safeAreaNormalized.yMin, cameraRect.yMin);
            float maxX = Mathf.Min(safeAreaNormalized.xMax, cameraRect.xMax);
            float maxY = Mathf.Min(safeAreaNormalized.yMax, cameraRect.yMax);

            if (cameraRect.width <= 0f || cameraRect.height <= 0f || maxX <= minX || maxY <= minY)
            {
                anchorMin = Vector2.zero;
                anchorMax = Vector2.one;
                return;
            }

            anchorMin = new Vector2((minX - cameraRect.xMin) / cameraRect.width, (minY - cameraRect.yMin) / cameraRect.height);
            anchorMax = new Vector2((maxX - cameraRect.xMin) / cameraRect.width, (maxY - cameraRect.yMin) / cameraRect.height);
        }

        private void Apply()
        {
            Rect area = DeviceScreen.safeArea;
            Rect cameraRect = CurrentCameraRect();
            _appliedArea = area;
            _appliedCameraRect = cameraRect;
            _appliedScreen = new Vector2Int(DeviceScreen.width, DeviceScreen.height);

            if (DeviceScreen.width <= 0 || DeviceScreen.height <= 0)
            {
                return;
            }

            var normalized = new Rect(
                area.x / DeviceScreen.width,
                area.y / DeviceScreen.height,
                area.width / DeviceScreen.width,
                area.height / DeviceScreen.height);

            CalculateAnchors(normalized, cameraRect, out Vector2 min, out Vector2 max);

            _rect.anchorMin = min;
            _rect.anchorMax = max;
            _rect.offsetMin = Vector2.zero;
            _rect.offsetMax = Vector2.zero;
        }
    }
}
