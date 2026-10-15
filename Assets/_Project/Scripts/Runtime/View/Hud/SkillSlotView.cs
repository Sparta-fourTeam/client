using System;
using DG.Tweening;
using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>스킬 한 칸: 아이콘, 쿨타임 원형 채움, 레벨. 스킬이 없는 칸은 빈 틀만 보인다.
    /// 쿨타임은 매 프레임 바뀌는 값이라 메시지를 받지 않고 Update에서 직접 읽는다</summary>
    public sealed class SkillSlotView : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private Image _icon;
        [SerializeField] private Image _cooldownOverlay;
        [SerializeField] private TMP_Text _levelText;
        [Tooltip("레벨 숫자 뒤의 배지. 스킬이 없는 칸에서는 숫자와 함께 숨긴다")]
        [SerializeField] private GameObject _levelBadge;
        [SerializeField] private Image _activeFrame;
        [SerializeField] private Sprite _activeSprite;
        [Tooltip("칸의 바탕. 스킬이 없는 칸은 비활성 색으로 살짝 어둡게 보인다")]
        [SerializeField] private Image _background;
        [SerializeField] private Color _emptyTint = new Color(0.74f, 0.72f, 0.82f, 1f);

        [Header("연출 (비워 두면 그 연출만 빠진다)")]
        [Tooltip("형태가 바뀌는 순간 아이콘 뒤에서 번쩍이는 빛 (흰색 소프트 원)")]
        [SerializeField] private Sprite _formFlashSprite;
        [Tooltip("형태가 바뀌는 순간 슬롯 둘레로 퍼지는 링 (흰색 얇은 원)")]
        [SerializeField] private Sprite _formRingSprite;

        private const float LevelPunchScale = 0.28f;
        private const float LevelPunchSeconds = 0.3f;
        private const float FormShrinkSeconds = 0.12f;
        private const float FormPopSeconds = 0.22f;
        private const float FormSettleSeconds = 0.14f;
        private const float FormFlashSeconds = 0.3f;
        private const float FormRingSeconds = 0.4f;

        private ISkillStatus _status;
        private bool _holding;
        private int _boundId;
        private int _boundLevel;
        private SkillForm _boundForm;
        private Tween _levelTween;
        private Tween _badgeTween;
        private Sequence _formSequence;
        private Image _formFlash;
        private Image _formRing;
        private Sequence _fxSequence;

        /// <summary>스킬이 든 칸을 누르기 시작했다 (사정거리 표시용)</summary>
        public event Action<ISkillStatus> HoldStarted;

        /// <summary>누르고 있던 손을 뗐다</summary>
        public event Action HoldEnded;

        private void Awake()
        {
            // 스코프 주입(LifetimeScope.Awake, 실행 순서 -5000)이 이 Awake보다 먼저라서, 버퍼에 남아 있던 SkillChanged가
            // 이미 Bind를 마쳤을 수 있다. 그때는 지우지 않는다. 아직 아무것도 못 받은 칸만 빈 틀로 만든다
            if (_status == null)
            {
                Clear();
            }
        }

        public void Bind(ISkillStatus status, Sprite icon)
        {
            // 같은 칸에 같은 스킬이 그대로 있을 때만 "올랐다/바뀌었다"로 본다. 새 스킬을 얻거나 칸이 바뀐 경우는 연출이 없다
            bool sameSkill = _status != null && _boundId == status.Id;
            bool levelUp = isActiveAndEnabled && sameSkill && status.Level > _boundLevel;
            bool formChanged = isActiveAndEnabled && sameSkill && status.Form != _boundForm && icon != null && _icon.enabled;
            _boundId = status.Id;
            _boundLevel = status.Level;
            _boundForm = status.Form;

            // 비활성 슬롯과 새 스킬은 즉시 반영한다. 같은 상태의 반복 갱신은 진행 중인 연출을 유지한다
            if (!sameSkill || !isActiveAndEnabled)
            {
                KillTweens();
            }
            else if (formChanged || icon == null)
            {
                _formSequence?.Kill();
                _fxSequence?.Kill();
            }
            _status = status;
            if (formChanged)
            {
                PlayTransform(icon);
            }
            else if (_formSequence == null || !_formSequence.IsActive())
            {
                _icon.sprite = icon;
                _icon.enabled = icon != null;
            }

            _levelText.text = status.Level.ToString();
            _levelText.gameObject.SetActive(true);
            if (_levelBadge != null) { _levelBadge.SetActive(true); }
            _cooldownOverlay.gameObject.SetActive(true);
            _cooldownOverlay.fillAmount = Mathf.Clamp01(status.CooldownRatio);

            _activeFrame.sprite = _activeSprite;
            _activeFrame.enabled = true;
            SetBackground(Color.white);

            if (levelUp)
            {
                PlayLevelPunch();
            }
        }

        public void Clear()
        {
            EndHold();
            KillTweens();
            _status = null;
            _boundId = 0;
            _icon.enabled = false;
            _levelText.gameObject.SetActive(false);
            if (_levelBadge != null) { _levelBadge.SetActive(false); }
            _cooldownOverlay.gameObject.SetActive(false);
            _activeFrame.enabled = false;
            SetBackground(_emptyTint);
        }

        // 숫자와 배지가 같이 튄다. 크기만 살짝 키웠다 돌아오게 해 과하지 않게 한다
        private void PlayLevelPunch()
        {
            _levelTween = Punch(_levelTween, _levelText.rectTransform);
            _badgeTween = Punch(_badgeTween, _levelBadge != null ? _levelBadge.transform as RectTransform : null);
        }

        private static Tween Punch(Tween running, RectTransform target)
        {
            if (target == null) { return null; }
            running?.Kill(true);
            return target.DOPunchScale(Vector3.one * LevelPunchScale, LevelPunchSeconds, 6, 0.6f)
                .SetUpdate(true).SetLink(target.gameObject);
        }

        // 아이콘이 쪼그라들며 사라지는 순간 새 아이콘으로 바뀌고, 커졌다 자리잡는다
        private void PlayTransform(Sprite next)
        {
            var rect = _icon.rectTransform;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;

            _formSequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
            _formSequence.Append(rect.DOScale(0.55f, FormShrinkSeconds).SetEase(Ease.InQuad));
            _formSequence.Join(rect.DOLocalRotate(new Vector3(0f, 0f, -18f), FormShrinkSeconds).SetEase(Ease.InQuad));
            _formSequence.AppendCallback(() =>
            {
                _icon.sprite = next;
                PlayTransformFx();
            });
            _formSequence.Append(rect.DOScale(1.3f, FormPopSeconds).SetEase(Ease.OutBack));
            _formSequence.Join(rect.DOLocalRotate(Vector3.zero, FormPopSeconds).SetEase(Ease.OutBack));
            _formSequence.Append(rect.DOScale(1f, FormSettleSeconds).SetEase(Ease.OutQuad));
            _formSequence.OnKill(() =>
            {
                // 취소 시 빛을 새로 만들지 않고 최신 아이콘과 기본 크기를 남긴다. 파괴 중에는 접근하지 않는다
                if (_icon != null) { _icon.sprite = next; }
                if (rect != null)
                {
                    rect.localScale = Vector3.one;
                    rect.localRotation = Quaternion.identity;
                }
            });
        }

        // 슬롯 둘레로 링이 퍼지고 아이콘 뒤에서 빛이 번쩍인다. 이미지는 한 번 만들어 재사용한다
        private void PlayTransformFx()
        {
            _fxSequence?.Kill();
            _fxSequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
            _formFlash = AddFlash(_formFlash, _formFlashSprite, FormFlashSeconds, 0.55f, 1.35f, 0.85f);
            _formRing = AddFlash(_formRing, _formRingSprite, FormRingSeconds, 0.6f, 1.5f, 0.9f);
            _fxSequence.OnKill(() =>
            {
                if (_formFlash != null) { _formFlash.enabled = false; }
                if (_formRing != null) { _formRing.enabled = false; }
            });
        }

        // 크기 from -> to로 커지며 투명해지는 한 번짜리 이미지. 반환값은 재사용할 이미지다
        private Image AddFlash(Image image, Sprite sprite, float seconds, float from, float to, float alpha)
        {
            if (sprite == null) { return image; }
            if (image == null)
            {
                var go = new GameObject(sprite.name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(_icon.transform.parent, false);
                go.transform.SetSiblingIndex(_icon.transform.GetSiblingIndex() + 1);
                var rect = (RectTransform)go.transform;
                rect.anchorMin = _icon.rectTransform.anchorMin;
                rect.anchorMax = _icon.rectTransform.anchorMax;
                rect.pivot = _icon.rectTransform.pivot;
                rect.anchoredPosition = _icon.rectTransform.anchoredPosition;
                rect.sizeDelta = _icon.rectTransform.sizeDelta;
                image = go.GetComponent<Image>();
                image.sprite = sprite;
                image.raycastTarget = false;
            }

            var target = image.rectTransform;
            image.enabled = true;
            image.color = new Color(1f, 1f, 1f, alpha);
            target.localScale = Vector3.one * from;
            _fxSequence.Join(target.DOScale(to, seconds).SetEase(Ease.OutQuad));
            _fxSequence.Join(DOTween.To(() => image.color.a, a => image.color = new Color(1f, 1f, 1f, a), 0f, seconds).SetEase(Ease.InQuad));
            return image;
        }

        private void KillTweens()
        {
            _levelTween?.Kill(true);
            _badgeTween?.Kill(true);
            _formSequence?.Kill();
            _fxSequence?.Kill();
            _levelTween = null;
            _badgeTween = null;
            _formSequence = null;
            _fxSequence = null;
        }

        private void SetBackground(Color color)
        {
            if (_background != null)
            {
                _background.color = color;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_status == null || _holding) { return; }
            _holding = true;
            HoldStarted?.Invoke(_status);
        }

        // 손을 슬롯 밖에서 떼도 누르기 시작한 칸으로 전달된다
        public void OnPointerUp(PointerEventData eventData) => EndHold();

        private void OnDisable()
        {
            EndHold();
            KillTweens();
        }

        private void EndHold()
        {
            if (!_holding) { return; }
            _holding = false;
            HoldEnded?.Invoke();
        }

        private void Update()
        {
            if (_status == null)
            {
                return;
            }

            // 카드 선택과 일시정지 중에는 timeScale이 0이라 쿨타임도 멈춘 채 그대로 보인다
            _cooldownOverlay.fillAmount = Mathf.Clamp01(_status.CooldownRatio);
        }
    }
}
