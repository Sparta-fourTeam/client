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
        [Tooltip("형태가 바뀌는 순간 아이콘 위에서 터지는 파티클 (UIParticle 프리팹). 슬롯 안에 한 번 만들어 재사용한다")]
        [SerializeField] private GameObject _transformFxPrefab;

        private const float LevelPunchScale = 0.28f;
        private const float LevelPunchSeconds = 0.3f;
        private const float FormShrinkSeconds = 0.12f;
        private const float FormPopSeconds = 0.22f;
        private const float FormSettleSeconds = 0.14f;
        private const float FormFxEmitSeconds = 0.12f;

        private ISkillStatus _status;
        private bool _holding;
        private int _boundId;
        private int _boundLevel;
        private SkillForm _boundForm;
        private Tween _levelTween;
        private Tween _badgeTween;
        private Sequence _formSequence;
        private Tween _fxStop;
        private ParticleSystem[] _transformFx;

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
            bool levelUp = sameSkill && status.Level > _boundLevel;
            bool formChanged = sameSkill && status.Form != _boundForm && icon != null && _icon.enabled;
            _boundId = status.Id;
            _boundLevel = status.Level;
            _boundForm = status.Form;

            // 진행 중인 변환 연출은 끝 상태로 마무리한다 (스프라이트 교체 콜백도 불린다)
            _formSequence?.Complete(true);
            _status = status;
            if (formChanged)
            {
                PlayTransform(icon);
            }
            else
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

        // 아이콘이 쪼그라들며 사라지는 순간 새 아이콘으로 바뀌고, 커졌다 자리잡는다. 바뀌는 순간 파티클이 터진다
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
                // 중간에 끊겨도 아이콘이 쪼그라든 채 남지 않게 한다
                rect.localScale = Vector3.one;
                rect.localRotation = Quaternion.identity;
            });
        }

        // 반복 재생 + 짧은 방출 후 중단 (UIParticle에서 확실히 그려지는 방식)
        private void PlayTransformFx()
        {
            if (_transformFxPrefab == null) { return; }
            if (_transformFx == null)
            {
                var fx = Instantiate(_transformFxPrefab, _icon.transform.parent);
                fx.transform.SetSiblingIndex(_icon.transform.GetSiblingIndex() + 1);
                var fxRect = fx.transform as RectTransform;
                if (fxRect != null) { fxRect.anchoredPosition = _icon.rectTransform.anchoredPosition; }
                _transformFx = fx.GetComponentsInChildren<ParticleSystem>(true);
            }

            foreach (var fx in _transformFx) { fx.Play(true); }
            _fxStop?.Kill();
            _fxStop = DOVirtual.DelayedCall(FormFxEmitSeconds, () =>
            {
                foreach (var fx in _transformFx) { fx.Stop(true, ParticleSystemStopBehavior.StopEmitting); }
            }, true).SetLink(gameObject);
        }

        private void KillTweens()
        {
            _levelTween?.Kill(true);
            _badgeTween?.Kill(true);
            _formSequence?.Kill(true);
            _fxStop?.Kill();
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
