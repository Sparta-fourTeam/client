using System;
using UnityEngine;

namespace Game.View
{
    /// <summary>팝업 패널 하나의 열기·닫기 전환. 패널 전체를 페이드하고 창(Window)은 살짝 커지며 나타난다.
    /// 시간은 timeScale과 상관없이(unscaled) 흐른다. 일시정지 창은 timeScale 0에서 열리기 때문이다.
    /// 연타해도 꼬이지 않는다: 열기·닫기를 번갈아 눌러도 지금 진행도에서 방향만 바뀌고, 닫는 동안에는 눌리지 않으며,
    /// 닫기가 끝나야 패널이 꺼진다. 이 컴포넌트를 거치지 않고 패널을 SetActive(true)로 켜면 전환 없이 바로 보인다.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class PopupTransition : MonoBehaviour
    {
        [Tooltip("커지며 나타날 창. 비우면 페이드만 한다")]
        [SerializeField] private RectTransform _window;
        [SerializeField, Min(0.01f)] private float _showSeconds = 0.18f;
        [SerializeField, Min(0.01f)] private float _hideSeconds = 0.12f;
        [Tooltip("완전히 숨었을 때 창의 크기 비율")]
        [SerializeField, Range(0.5f, 1f)] private float _hiddenScale = 0.92f;

        private CanvasGroup _group;
        private float _progress;      // 0 = 완전히 숨음, 1 = 완전히 보임
        private bool _target;         // 열려 있어야 하는가
        private bool _hiding;         // 닫는 전환이 진행 중인가
        private Action _onHidden;

        /// <summary>열려 있거나 여는 중인가 (닫는 중이면 false)</summary>
        public bool IsShown => _target;

        /// <summary>0(완전히 숨음)~1(완전히 보임)</summary>
        public float Progress => _progress;

        private CanvasGroup Group => _group != null ? _group : _group = GetComponent<CanvasGroup>();

        public void Show()
        {
            _target = true;
            _hiding = false;
            _onHidden = null;
            gameObject.SetActive(true);
            Group.interactable = true;
            Group.blocksRaycasts = true;
            Apply();
        }

        /// <summary>닫는다. 닫기가 끝나 패널이 꺼진 뒤 onHidden을 부른다. 이미 닫혀 있으면 바로 부른다</summary>
        public void Hide(Action onHidden = null)
        {
            if (!gameObject.activeSelf)
            {
                onHidden?.Invoke();
                return;
            }

            _onHidden += onHidden;
            if (_hiding)
            {
                return;
            }

            _target = false;
            _hiding = true;
            Group.interactable = false;
            Group.blocksRaycasts = false;
        }

        /// <summary>전환 없이 바로 끄고 처음 상태로 되돌린다</summary>
        public void HideImmediate()
        {
            _target = false;
            _hiding = false;
            _progress = 0f;
            _onHidden = null;
            Apply();
            gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            // Show를 거치지 않고 켜졌으면(예: 다른 코드의 SetActive(true)) 전환 없이 바로 완전히 보인다
            if (!_target && !_hiding)
            {
                _target = true;
                _progress = 1f;
                Group.interactable = true;
                Group.blocksRaycasts = true;
                Apply();
            }
        }

        private void OnDisable()
        {
            // 꺼지면 다음에 켜질 때를 위해 처음 상태로 돌린다 (끝나지 않은 닫기 콜백은 버린다)
            _target = false;
            _hiding = false;
            _progress = 0f;
            _onHidden = null;
        }

        private void Update() => Tick(Time.unscaledDeltaTime);

        internal void Tick(float deltaTime)
        {
            if (!_target && !_hiding)
            {
                return;
            }

            float step = deltaTime / (_target ? _showSeconds : _hideSeconds);
            _progress = _target ? Mathf.Min(1f, _progress + step) : Mathf.Max(0f, _progress - step);
            Apply();

            if (!_target && _progress <= 0f)
            {
                _hiding = false;
                var callback = _onHidden;
                _onHidden = null;
                gameObject.SetActive(false);
                callback?.Invoke();
            }
        }

        private void Apply()
        {
            float eased = 1f - (1f - _progress) * (1f - _progress) * (1f - _progress);   // ease-out cubic
            Group.alpha = _progress;
            if (_window != null)
            {
                _window.localScale = Vector3.one * Mathf.Lerp(_hiddenScale, 1f, eased);
            }
        }
    }

    /// <summary>패널을 켜고 끄는 한 곳. PopupTransition이 있으면 전환으로, 없으면 바로 켜고 끈다 (씬에 아직 연결하지 않은 경우도 동작한다)</summary>
    internal static class PopupPanel
    {
        public static void Set(GameObject panel, PopupTransition transition, bool visible)
        {
            if (transition == null)
            {
                panel.SetActive(visible);
            }
            else if (visible)
            {
                transition.Show();
            }
            else
            {
                transition.Hide();
            }
        }
    }
}
