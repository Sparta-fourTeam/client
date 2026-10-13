using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>팝업 패널 하나의 열기·닫기 전환. 패널 전체를 페이드하고 창(Window)은 살짝 커지며 나타난다.
    /// 시간은 timeScale과 상관없이(unscaled) 흐른다. 일시정지 창은 timeScale 0에서 열리기 때문이다.
    /// 연타해도 꼬이지 않는다: 열기·닫기를 번갈아 눌러도 지금 상태에서 방향만 바뀌고, 닫는 동안에는 눌리지 않으며,
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
        private Sequence _sequence;
        private bool _target;         // 열려 있어야 하는가
        private bool _hiding;         // 닫는 전환이 진행 중인가
        private Action _onHidden;

        /// <summary>열려 있거나 여는 중인가 (닫는 중이면 false)</summary>
        public bool IsShown => _target;

        /// <summary>0(완전히 숨음)~1(완전히 보임)</summary>
        public float Progress => Group.alpha;

        private CanvasGroup Group => _group != null ? _group : _group = GetComponent<CanvasGroup>();

        public void Show()
        {
            _target = true;
            _hiding = false;
            _onHidden = null;
            if (!gameObject.activeSelf)
            {
                ResetToHidden();   // 꺼져 있던 패널은 프리팹에 저장된 상태가 아니라 완전히 숨은 상태에서 시작한다
                gameObject.SetActive(true);
            }

            Group.interactable = true;
            Group.blocksRaycasts = true;

            Play(1f, _showSeconds, Ease.OutCubic, null);
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
            Play(0f, _hideSeconds, Ease.InCubic, Finish);
        }

        /// <summary>전환 없이 바로 끄고 처음 상태로 되돌린다</summary>
        public void HideImmediate()
        {
            _target = false;
            _hiding = false;
            _onHidden = null;
            ResetToHidden();
            gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            // Show를 거치지 않고 켜졌으면(예: 다른 코드의 SetActive(true)) 전환 없이 바로 완전히 보인다
            if (!_target && !_hiding)
            {
                _target = true;
                Group.alpha = 1f;
                SetWindowScale(1f);
                Group.interactable = true;
                Group.blocksRaycasts = true;
            }
        }

        private void OnDisable()
        {
            // 꺼지면 다음에 켜질 때를 위해 처음 상태로 돌린다 (끝나지 않은 닫기 콜백은 버린다)
            _target = false;
            _hiding = false;
            _onHidden = null;
            ResetToHidden();
        }

        private void OnDestroy() => _sequence?.Kill();

        /// <summary>지금 값에서 목표까지 간다. 남은 거리에 비례해 시간을 줄여 중간에 방향을 바꿔도 튀지 않는다</summary>
        private void Play(float alphaTarget, float seconds, Ease ease, TweenCallback onComplete)
        {
            _sequence?.Kill();

            float remaining = Mathf.Abs(alphaTarget - Group.alpha);
            float duration = Mathf.Max(0.0001f, seconds * remaining);
            float scaleTarget = Mathf.Lerp(_hiddenScale, 1f, alphaTarget);

            _sequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
            _sequence.Join(DOTween.To(() => Group.alpha, a => Group.alpha = a, alphaTarget, duration).SetEase(Ease.Linear));
            if (_window != null)
            {
                _sequence.Join(_window.DOScale(Vector3.one * scaleTarget, duration).SetEase(ease));
            }

            if (onComplete != null)
            {
                _sequence.OnComplete(onComplete);
            }
        }

        private void Finish()
        {
            _hiding = false;
            var callback = _onHidden;
            _onHidden = null;
            gameObject.SetActive(false);
            callback?.Invoke();
        }

        private void ResetToHidden()
        {
            _sequence?.Kill();
            _sequence = null;
            Group.alpha = 0f;
            SetWindowScale(_hiddenScale);
        }

        private void SetWindowScale(float scale)
        {
            if (_window != null)
            {
                _window.localScale = Vector3.one * scale;
            }
        }
    }

    /// <summary>패널을 켜고 끄는 한 곳. PopupTransition이 있으면 전환으로, 없으면 바로 켜고 끈다 (씬에 아직 연결하지 않은 경우도 동작한다)</summary>
    internal static class PopupPanel
    {
        /// <summary>보상 목록 길이에 맞춰 창 높이를 정하고, 긴 목록은 스크롤 영역에 남긴다.</summary>
        public static void FitContent(ScrollRect scroll, float minHeight, float maxHeight)
        {
            Canvas.ForceUpdateCanvases();
            var window = (RectTransform)scroll.transform.parent;
            var safeArea = (RectTransform)window.parent;
            float available = safeArea.rect.height > 96f ? safeArea.rect.height - 96f : maxHeight;
            var viewport = (RectTransform)scroll.transform;
            float chrome = viewport.offsetMin.y - viewport.offsetMax.y;
            float preferred = LayoutUtility.GetPreferredHeight(scroll.content) + chrome;
            window.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                Mathf.Clamp(preferred, Mathf.Min(minHeight, available), Mathf.Min(maxHeight, available)));
            Canvas.ForceUpdateCanvases();
        }

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
