using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>이 버튼(과 자식 버튼)은 눌러도 크기가 변하지 않는다. 자체 애니메이션이 있는 버튼 등에 붙인다</summary>
    [DisallowMultipleComponent]
    public sealed class NoPressFeedback : MonoBehaviour
    {
    }

    /// <summary>눌린 대상 하나를 살짝 줄였다가 놓으면 되돌린다. 시간은 타임스케일과 상관없이(unscaled) 흐른다.
    /// 놓은 뒤에도 대상이 꺼졌거나 파괴돼도 크기를 원래대로 돌려, 꺼진 팝업의 버튼이 줄어든 채 남지 않게 한다</summary>
    public sealed class PressScaleAnimator
    {
        private readonly float _pressedScale;
        private readonly float _pressSeconds;
        private readonly float _releaseSeconds;
        private Transform _target;
        private Vector3 _baseScale;
        private Tween _tween;

        public PressScaleAnimator(float pressedScale = 0.95f, float pressSeconds = 0.06f, float releaseSeconds = 0.10f)
        {
            _pressedScale = pressedScale;
            _pressSeconds = pressSeconds;
            _releaseSeconds = releaseSeconds;
        }

        public bool IsActive => _target != null;

        /// <summary>진행 중인 트윈 (테스트가 시간을 앞당길 때 쓴다)</summary>
        public Tween Current => _tween;

        /// <summary>target을 누른다. 앞서 놓는 중이던 대상은 바로 원래 크기로 돌린다</summary>
        public void Press(Transform target)
        {
            Restore();
            if (target == null)
            {
                return;
            }

            _target = target;
            _baseScale = target.localScale;
            _tween = target.DOScale(_baseScale * _pressedScale, _pressSeconds).SetEase(Ease.OutQuad).SetUpdate(true);
        }

        public void Release()
        {
            if (_target == null)
            {
                return;
            }

            _tween?.Kill();
            _tween = _target.DOScale(_baseScale, _releaseSeconds).SetEase(Ease.OutQuad).SetUpdate(true).OnComplete(Clear);
        }

        /// <summary>대상이 꺼졌거나 사라졌으면 크기를 되돌리고 놓는다. 매 프레임 불러도 되는 가벼운 검사</summary>
        public void CheckTarget()
        {
            if (_target == null)
            {
                if (_tween != null)
                {
                    _tween.Kill();
                    Clear();
                }

                return;
            }

            if (!_target.gameObject.activeInHierarchy)
            {
                Restore();
            }
        }

        private void Restore()
        {
            _tween?.Kill();
            if (_target != null)
            {
                _target.localScale = _baseScale;
            }

            Clear();
        }

        private void Clear()
        {
            _tween = null;
            _target = null;
        }
    }

    /// <summary>모든 화면의 버튼에 눌림 피드백(살짝 줄었다 되돌아옴)을 준다. 프리팹마다 붙이지 않고 한 곳에서 처리하므로
    /// 실행 중에 만들어지는 버튼과 앞으로 추가하는 버튼도 자동으로 적용된다. 끄고 싶은 버튼에는 NoPressFeedback을 붙인다.
    /// 눌린 위치에서 이벤트 시스템이 고르는 대상(가장 위의 클릭 가능한 오브젝트)이 눌릴 수 있는 Button일 때만 반응한다</summary>
    public sealed class ButtonPressFeedback : MonoBehaviour
    {
        private readonly PressScaleAnimator _animator = new();
        private readonly List<RaycastResult> _hits = new();
        private PointerEventData _eventData;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindFirstObjectByType<ButtonPressFeedback>() != null)
            {
                return;
            }

            var go = new GameObject(nameof(ButtonPressFeedback));
            DontDestroyOnLoad(go);
            go.AddComponent<ButtonPressFeedback>();
        }

        private void Update()
        {
            var pointer = Pointer.current;
            if (pointer != null)
            {
                if (pointer.press.wasPressedThisFrame)
                {
                    Press(pointer.position.ReadValue());
                }

                if (pointer.press.wasReleasedThisFrame)
                {
                    _animator.Release();
                }
            }

            _animator.CheckTarget();
        }

        /// <summary>화면 좌표를 누른다. 눌린 Button이 있으면 돌려주고 그 버튼을 줄이기 시작한다</summary>
        internal Button Press(Vector2 screenPosition)
        {
            var button = FindButton(screenPosition);
            if (button != null && button.GetComponentInParent<NoPressFeedback>() == null)
            {
                _animator.Press(button.transform);
                return button;
            }

            return null;
        }

        internal void ReleasePress() => _animator.Release();

        private Button FindButton(Vector2 screenPosition)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return null;
            }

            _eventData ??= new PointerEventData(eventSystem);
            _eventData.position = screenPosition;
            _hits.Clear();
            eventSystem.RaycastAll(_eventData, _hits);

            // 이벤트 시스템과 같은 규칙: 가장 위에서 맞은 오브젝트가 클릭을 받는 대상이 된다
            foreach (var hit in _hits)
            {
                if (hit.gameObject == null)
                {
                    continue;
                }

                var handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit.gameObject);
                var button = handler != null ? handler.GetComponent<Button>() : null;
                return button != null && button.IsInteractable() ? button : null;
            }

            return null;
        }
    }
}
