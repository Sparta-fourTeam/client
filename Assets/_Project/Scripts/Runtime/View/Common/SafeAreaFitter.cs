using UnityEngine;

namespace Game.View
{
    /// <summary>RectTransform을 기기의 Safe Area(노치·펀치홀·홈 인디케이터를 뺀 영역)에 맞춘다.
    /// 부모는 화면 전체로 늘어나 있어야 한다. 회전·해상도 변경에도 다시 맞춘다</summary>
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

        private void Update()
        {
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
