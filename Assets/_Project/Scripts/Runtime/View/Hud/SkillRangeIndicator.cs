using Game.Core;
using UnityEngine;

namespace Game.View
{
    /// <summary>스킬을 누르고 있는 동안 사정거리를 점선으로 보여 준다. 반원(플레이어 기준)과 가로선(벽 앞선 기준) 두 모양이며,
    /// 점선 모양은 LineRenderer에 연결한 점선 텍스처(타일)가 만든다.</summary>
    public sealed class SkillRangeIndicator : MonoBehaviour
    {
        private const int ArcSegments = 64;
        /// <summary>선의 양 끝이 화면 밖으로 충분히 나가도록 카메라를 못 찾을 때 쓰는 가로 반폭</summary>
        private const float FallbackHalfWidth = 4f;

        [SerializeField] private LineRenderer _line;
        /// <summary>점선 텍스처 한 칸(점 하나와 빈칸)의 월드 길이</summary>
        [SerializeField] private float _dashPeriod = .4f;

        private void Awake()
        {
            if (_line != null)
            {
                _line.textureMode = LineTextureMode.Tile;
                _line.textureScale = new Vector2(1f / Mathf.Max(.01f, _dashPeriod), 1f);
                _line.enabled = false;
            }
        }

        public bool IsShown => _line != null && _line.enabled;

        public void Show(SkillRange range)
        {
            if (_line == null) { return; }
            var points = range.Shape == SkillRangeShape.Arc
                ? ArcPoints(range.Center, range.Radius, ArcSegments)
                : LinePoints(range.LineY, CameraCenterX(), CameraHalfWidth());
            _line.positionCount = points.Length;
            _line.SetPositions(points);
            _line.enabled = true;
        }

        public void Hide()
        {
            if (_line != null) { _line.enabled = false; }
        }

        /// <summary>중심에서 위쪽 반원(오른쪽 끝에서 왼쪽 끝 방향)의 점들</summary>
        public static Vector3[] ArcPoints(Vector2 center, float radius, int segments)
        {
            var points = new Vector3[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                float angle = Mathf.PI * i / segments;
                points[i] = new Vector3(center.x + Mathf.Cos(angle) * radius, center.y + Mathf.Sin(angle) * radius, 0);
            }
            return points;
        }

        /// <summary>높이 y에서 가운데 x를 기준으로 양쪽 halfWidth만큼 뻗는 가로선의 두 끝점</summary>
        public static Vector3[] LinePoints(float y, float centerX, float halfWidth) =>
            new[] { new Vector3(centerX - halfWidth, y, 0), new Vector3(centerX + halfWidth, y, 0) };

        private static float CameraCenterX() => Camera.main != null ? Camera.main.transform.position.x : 0f;

        private static float CameraHalfWidth() =>
            Camera.main != null && Camera.main.orthographic ? Camera.main.orthographicSize * Camera.main.aspect : FallbackHalfWidth;
    }
}
