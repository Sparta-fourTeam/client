using UnityEngine;

namespace Game.View
{
    /// <summary>RectTransform을 기기의 Safe Area(노치·펀치홀·홈 인디케이터를 뺀 영역)에 맞춘다.
    /// 부모는 화면 전체로 늘어나 있어야 한다. 로비 UI 전체를 담는 최상위 컨테이너에 하나만 붙인다.
    /// Unity에는 해상도 변경 이벤트가 없으므로, 부모 캔버스 크기가 바뀔 때 불리는 OnRectTransformDimensionsChange로 다시 맞춘다.
    /// 해상도는 그대로이고 Safe Area만 바뀌는 경우(에디터 Simulator에서 기기만 바꿀 때)는 다시 맞추지 않는다</summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform _rect;
        private Rect _appliedArea;
        private Vector2Int _appliedScreen;

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

            if (Screen.safeArea != _appliedArea || Screen.width != _appliedScreen.x || Screen.height != _appliedScreen.y)
            {
                Apply();
            }
        }

        private void Apply()
        {
            Rect area = Screen.safeArea;
            _appliedArea = area;
            _appliedScreen = new Vector2Int(Screen.width, Screen.height);

            if (Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }

            Vector2 min = area.position;
            Vector2 max = area.position + area.size;
            min.x /= Screen.width;
            min.y /= Screen.height;
            max.x /= Screen.width;
            max.y /= Screen.height;

            _rect.anchorMin = min;
            _rect.anchorMax = max;
            _rect.offsetMin = Vector2.zero;
            _rect.offsetMax = Vector2.zero;
        }
    }
}
