using UnityEngine;
using DeviceScreen = UnityEngine.Device.Screen;

namespace Game.View
{
    /// <summary>전투 카메라가 화면 비율과 상관없이 같은 크기의 월드를 보게, 카메라가 그리는 영역(Viewport)을 목표 비율로 맞춘다.
    /// Orthographic Size는 건드리지 않는다. 월드를 얼마나 볼지는 크기가 정하고, 그것을 화면 어디에 그릴지가 Viewport다.
    /// 화면이 목표 비율보다 길면 위아래, 넓으면 좌우에 빈 영역이 생긴다. 카메라 rect 밖은 아무도 지우지 않으므로
    /// 전체 화면을 먼저 그리는 Background Camera(Depth가 더 낮은 카메라)를 따로 두어 그 영역을 채운다.
    /// 화면 크기는 SafeAreaFitter처럼 UnityEngine.Device.Screen으로 읽는다 (Device Simulator에서도 기기 값)</summary>
    [RequireComponent(typeof(Camera))]
    public sealed class AspectRatioCamera : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float _targetWidth = 9f;
        [SerializeField, Min(1f)] private float _targetHeight = 16f;

        private Camera _camera;
        private Vector2Int _appliedScreen;

        private float TargetAspect => _targetWidth / _targetHeight;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
        }

        private void OnEnable()
        {
            Apply();
        }

        // 해상도 변경 이벤트가 없어서 크기가 달라졌는지 매 프레임 비교한다 (비교만 하고 같으면 아무것도 안 한다)
        private void Update()
        {
            if (DeviceScreen.width != _appliedScreen.x || DeviceScreen.height != _appliedScreen.y)
            {
                Apply();
            }
        }

        /// <summary>목표 비율의 영역이 화면 가운데에 놓이도록 Viewport(0~1)를 계산한다.
        /// 화면 크기나 목표 비율이 0 이하이면 화면 전체를 돌려준다</summary>
        public static Rect CalculateViewport(float screenWidth, float screenHeight, float targetAspect)
        {
            if (screenWidth <= 0f || screenHeight <= 0f || targetAspect <= 0f)
            {
                return new Rect(0f, 0f, 1f, 1f);
            }

            float screenAspect = screenWidth / screenHeight;

            if (Mathf.Approximately(screenAspect, targetAspect))
            {
                return new Rect(0f, 0f, 1f, 1f);
            }

            if (screenAspect > targetAspect)
            {
                // 목표보다 넓은 화면: 세로는 다 쓰고 가로를 줄여 좌우에 빈 영역
                float width = targetAspect / screenAspect;
                return new Rect((1f - width) * 0.5f, 0f, width, 1f);
            }

            // 목표보다 긴 화면: 가로는 다 쓰고 세로를 줄여 위아래에 빈 영역
            float height = screenAspect / targetAspect;
            return new Rect(0f, (1f - height) * 0.5f, 1f, height);
        }

        private void Apply()
        {
            if (_camera == null)
            {
                _camera = GetComponent<Camera>();
            }

            _appliedScreen = new Vector2Int(DeviceScreen.width, DeviceScreen.height);
            _camera.rect = CalculateViewport(_appliedScreen.x, _appliedScreen.y, TargetAspect);
        }
    }
}
