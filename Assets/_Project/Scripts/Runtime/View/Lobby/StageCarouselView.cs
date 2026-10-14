using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.View
{
    /// <summary>스테이지 선택창의 관문 카드를 좌우로 넘긴다. 가운데 카드 양옆에 이전·다음 관문 카드를 조금 작고 흐리게 살짝 보여 주고,
    /// 끌면 카드들이 따라 움직이다가 놓으면 다음·이전 관문으로 넘어가거나 제자리로 돌아온다. 화살표(Slide)도 같은 움직임으로 넘긴다.
    /// 양옆 카드는 가운데 카드를 복제한 미리보기라 그리기만 하고 터치는 받지 않는다.
    /// 넘기는 중에 다시 넘기면 지금 움직임을 바로 끝내고 다음 관문으로 이어서 넘긴다</summary>
    public sealed class StageCarouselView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private StageCardView _center;
        [Tooltip("양옆 카드 크기 (가운데 = 1)")]
        [SerializeField, Range(0.5f, 1f)] private float _sideScale = 0.85f;
        [Tooltip("양옆 카드 투명도 (가운데 = 1)")]
        [SerializeField, Range(0f, 1f)] private float _sideAlpha = 0.6f;
        [Tooltip("가운데 카드와 옆 카드 사이 간격(px)")]
        [SerializeField] private float _gap = 40f;
        [Tooltip("카드 간격의 몇 배 이상 끌면 넘어가는지")]
        [SerializeField, Range(0.05f, 0.5f)] private float _swipeThreshold = 0.2f;
        [SerializeField] private float _slideSeconds = 0.15f;

        /// <summary>그 관문이 있는지. 없는 쪽은 미리보기를 숨기고 그쪽으로는 넘기지 않는다</summary>
        public Func<int, bool> StageExists = _ => false;
        /// <summary>지금 넘길 수 있는지 (입장·수령 중이 아님)</summary>
        public Func<bool> CanSlide = () => true;
        /// <summary>넘기는 움직임이 끝났을 때 delta(-1 이전, +1 다음)로 불린다. 받은 쪽이 관문을 바꾸고 Show로 다시 그린다</summary>
        public Action<int> Moved;
        /// <summary>미리보기 카드에 그 관문의 내용을 그린다</summary>
        public Action<StageCardView, int> BindPreview;

        private StageCardView _prev, _next;
        private CanvasGroup _centerGroup, _prevGroup, _nextGroup;
        private float _offset;
        private bool _dragging;
        private Tween _slide;
        private int _stageId;

        public bool IsSliding => _slide != null && _slide.IsActive();

        /// <summary>stageId를 가운데에 두고 양옆 미리보기를 그린다</summary>
        public void Show(int stageId)
        {
            EnsurePreviews();
            _stageId = stageId;
            BindSide(_prev, stageId - 1);
            BindSide(_next, stageId + 1);
            if (!_dragging && !IsSliding)
            {
                Layout(0f);
            }
        }

        /// <summary>화살표: delta 쪽 카드가 가운데로 미끄러져 온 뒤 넘어간다. 연달아 누르면 앞 움직임을 끝내고 바로 이어 간다</summary>
        public void Slide(int delta)
        {
            if (_dragging)
            {
                return;
            }

            FinishSlide();
            if (!CanGo(delta))
            {
                return;
            }

            AnimateTo(-delta * Spacing, () => Moved?.Invoke(delta));
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            FinishSlide();
            _dragging = CanSlide();
        }

        // 움직이는 중이면 끝 위치로 보내고 완료 처리(관문 바꾸기)까지 바로 한다
        private void FinishSlide()
        {
            if (IsSliding)
            {
                _slide.Complete(true);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging)
            {
                return;
            }

            var canvas = GetComponentInParent<Canvas>();
            float dx = eventData.delta.x / (canvas != null ? canvas.rootCanvas.scaleFactor : 1f);
            float next = _offset + dx;
            // 넘어갈 관문이 없는 쪽으로는 덜 끌린다
            if (!StageExists(next < 0f ? _stageId + 1 : _stageId - 1))
            {
                next = _offset + dx * 0.3f;
            }

            float spacing = Spacing;
            Layout(Mathf.Clamp(next, -spacing, spacing));
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_dragging)
            {
                return;
            }

            _dragging = false;
            float threshold = Spacing * _swipeThreshold;
            int delta = _offset <= -threshold ? +1 : _offset >= threshold ? -1 : 0;
            if (delta != 0 && CanGo(delta))
            {
                AnimateTo(-delta * Spacing, () => Moved?.Invoke(delta));
            }
            else
            {
                AnimateTo(0f, null);
            }
        }

        private bool CanGo(int delta) => CanSlide() && StageExists(_stageId + delta);

        private float Spacing => ((RectTransform)_center.transform).rect.width * (1f + _sideScale) * 0.5f + _gap;

        private void AnimateTo(float target, Action done)
        {
            _slide?.Kill();
            _slide = DOTween.To(() => _offset, Layout, target, _slideSeconds).SetEase(Ease.OutCubic).SetUpdate(true).SetLink(gameObject)
                .OnComplete(() =>
                {
                    _slide = null;
                    // 관문이 바뀌면 받은 쪽이 Show로 새 관문을 그리고, 카드는 가운데로 돌아온다
                    done?.Invoke();
                    Layout(0f);
                });
        }

        private void Layout(float offset)
        {
            _offset = offset;
            if (_prev == null)
            {
                return;
            }

            float spacing = Spacing;
            Place(_center.transform, _centerGroup, offset, spacing);
            Place(_prev.transform, _prevGroup, offset - spacing, spacing);
            Place(_next.transform, _nextGroup, offset + spacing, spacing);
        }

        // 가운데에서 멀어질수록 작고 흐려진다
        private void Place(Transform card, CanvasGroup group, float x, float spacing)
        {
            var rect = (RectTransform)card;
            rect.anchoredPosition = new Vector2(x, rect.anchoredPosition.y);
            float t = spacing > 0f ? Mathf.Clamp01(Mathf.Abs(x) / spacing) : 0f;
            rect.localScale = Vector3.one * Mathf.Lerp(1f, _sideScale, t);
            group.alpha = Mathf.Lerp(1f, _sideAlpha, t);
        }

        private void BindSide(StageCardView card, int stageId)
        {
            bool exists = StageExists(stageId);
            card.gameObject.SetActive(exists);
            if (exists)
            {
                BindPreview?.Invoke(card, stageId);
            }
        }

        private void EnsurePreviews()
        {
            if (_prev != null)
            {
                return;
            }

            _centerGroup = Group(_center.gameObject, true);
            _prev = CreatePreview("PrevPreview");
            _next = CreatePreview("NextPreview");
            _prevGroup = _prev.GetComponent<CanvasGroup>();
            _nextGroup = _next.GetComponent<CanvasGroup>();
        }

        private StageCardView CreatePreview(string previewName)
        {
            var preview = Instantiate(_center, _center.transform.parent, false);
            preview.name = previewName;
            // 가운데 카드 아래에 그린다 (가운데 카드·화살표가 위)
            preview.transform.SetSiblingIndex(_center.transform.GetSiblingIndex());
            Group(preview.gameObject, false);
            return preview;
        }

        private static CanvasGroup Group(GameObject target, bool interactive)
        {
            var group = target.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = target.AddComponent<CanvasGroup>();
            }

            group.interactable = interactive;
            group.blocksRaycasts = interactive;
            return group;
        }

        private void OnDisable()
        {
            // 화면이 닫히면 움직임을 멈추고 가운데로 돌려놓는다
            _slide?.Kill();
            _slide = null;
            _dragging = false;
            Layout(0f);
        }
    }
}
