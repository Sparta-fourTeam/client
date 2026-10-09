using System;
using System.Collections.Generic;
using DG.Tweening;
using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>카드 선택 화면의 카드 한 장. 표시와 클릭만 맡고, 무엇을 고르는지는 CardSelectView가 안다</summary>
    public sealed class CardSlotView : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _levelText;
        [SerializeField] private TMP_Text _descText;

        private int _index;
        private Action<int> _onPick;
        [SerializeField] private Image _background;
        [SerializeField] private Image _iconImage;
        [Tooltip("카드 아래 형태 변환 아이콘 칸. 관련 변환이 칸보다 많으면 앞에서부터 칸 수만큼만 보인다")]
        [SerializeField] private FormHintIconView[] _formHints;


        private const float EntranceSeconds = 0.15f;
        private const float EntranceStartScaleY = 0.15f;
        private const float EntranceSlide = 35f;       // 펼쳐지는 동안 위에서 이만큼 내려온다 (기준 해상도 px)
        private const float FoldSeconds = 0.08f;
        private const float PickedHoldSeconds = 0.35f;
        private const float PickedFadeSeconds = 0.2f;
        private const float PickedSink = 35f;
        private const float PickedGrowScale = 1.15f;
        private static readonly Color PickedTint = new Color(0.45f, 0.45f, 0.45f);

        private Sequence _entrance;
        private Sequence _picked;
        private CanvasGroup _group;
        private RectTransform _rect;
        private Graphic[] _graphics;

        private void Awake()
        {
            _button.onClick.AddListener(() => _onPick?.Invoke(_index));
            // 세로로 펼칠 때 위쪽 가장자리가 고정되도록 피벗을 위로 둔다. 레이아웃 그룹은 피벗을 감안해 배치한다
            _rect = (RectTransform)transform;
            _rect.pivot = new Vector2(_rect.pivot.x, 1f);
        }

        /// <summary>카드가 폭은 그대로 두고 위쪽 가장자리에서 아래로 펼쳐지며 살짝 내려앉는다. delay를 카드마다 다르게 주면 차례로 나온다.
        /// 패널이 켜진 뒤에 불러야 한다: 레이아웃을 먼저 계산해 도착 위치를 얻는다.
        /// 등장하는 동안에는 눌리지 않는다. 일시정지 중에도 흐르도록 unscaled 시간을 쓴다</summary>
        public void PlayEntrance(float delay)
        {
            Stop();
            EnsureGroup();
            _rect = (RectTransform)transform;
            RestoreTint();

            // 도착 위치는 레이아웃이 정한 값이다. 계산을 끝낸 뒤에 읽어야 겹치지 않는다
            if (_rect.parent is RectTransform parent)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(parent);
            }
            var home = _rect.anchoredPosition;

            _group.alpha = 0f;
            _group.interactable = false;
            _rect.localScale = new Vector3(1f, EntranceStartScaleY, 1f);
            _rect.anchoredPosition = home + new Vector2(0f, EntranceSlide);

            _entrance = DOTween.Sequence().SetDelay(delay).SetUpdate(true).SetLink(gameObject);
            _entrance.Join(DOTween.To(() => _group.alpha, a => _group.alpha = a, 1f, EntranceSeconds * 0.4f));
            _entrance.Join(DOTween.To(() => _rect.localScale.y, y => _rect.localScale = new Vector3(1f, y, 1f), 1f, EntranceSeconds)
                .SetEase(Ease.OutQuad));
            _entrance.Join(DOTween.To(() => _rect.anchoredPosition, v => _rect.anchoredPosition = v, home, EntranceSeconds)
                .SetEase(Ease.OutQuad));
            _entrance.OnComplete(() => _group.interactable = true);
        }

        /// <summary>이 카드가 골라졌다. 잠시 남아 아래로 내려앉다가, 조금 커지며 회색으로 흐려져 사라진다</summary>
        public void PlayPicked()
        {
            Prepare();
            CollectGraphics();
            var start = _rect.anchoredPosition;
            _picked = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
            _picked.Join(DOTween.To(() => _rect.anchoredPosition, v => _rect.anchoredPosition = v,
                start - new Vector2(0f, PickedSink), PickedHoldSeconds + PickedFadeSeconds).SetEase(Ease.Linear));
            _picked.Insert(PickedHoldSeconds, _rect.DOScale(PickedGrowScale, PickedFadeSeconds).SetEase(Ease.OutQuad));
            _picked.Insert(PickedHoldSeconds, DOTween.To(() => 0f, t => Tint(t), 1f, PickedFadeSeconds));
            _picked.Insert(PickedHoldSeconds, DOTween.To(() => _group.alpha, a => _group.alpha = a, 0f, PickedFadeSeconds));
        }

        /// <summary>다른 카드가 골라졌다. 위로 접혀 사라진다. 자리는 그대로 두어 고른 카드가 움직이지 않게 한다</summary>
        public void PlayDismissed()
        {
            Prepare();
            _picked = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
            _picked.Join(DOTween.To(() => _rect.localScale.y, y => _rect.localScale = new Vector3(1f, y, 1f), 0f, FoldSeconds)
                .SetEase(Ease.InQuad));
            _picked.OnComplete(() => _group.alpha = 0f);
        }

        private void Stop()
        {
            _entrance?.Kill();
            _picked?.Kill();
        }

        private void CollectGraphics()
        {
            _graphics ??= GetComponentsInChildren<Graphic>(true);
            _tintFrom = new Color[_graphics.Length];
            for (int i = 0; i < _graphics.Length; i++) { _tintFrom[i] = _graphics[i].color; }
        }

        private Color[] _tintFrom;

        private void Tint(float t)
        {
            if (_graphics == null) { return; }
            for (int i = 0; i < _graphics.Length; i++)
            {
                var from = _tintFrom[i];
                var to = new Color(from.r * PickedTint.r, from.g * PickedTint.g, from.b * PickedTint.b, from.a);
                _graphics[i].color = Color.Lerp(from, to, t);
            }
        }

        // 다음 등장 전에 색을 원래대로 돌린다
        private void RestoreTint()
        {
            if (_graphics == null || _tintFrom == null) { return; }
            for (int i = 0; i < _graphics.Length; i++) { _graphics[i].color = _tintFrom[i]; }
            _tintFrom = null;
        }

        // Unity 오브젝트는 ??로 null을 판별하면 안 된다 (없는 컴포넌트가 가짜 null로 돌아온다)
        private void EnsureGroup()
        {
            if (_group != null) { return; }
            if (!TryGetComponent(out _group))
            {
                _group = gameObject.AddComponent<CanvasGroup>();
            }
        }

        // 등장 연출이 끝나지 않았다면 끝 상태로 만든 뒤 시작한다
        private void Prepare()
        {
            _entrance?.Complete();
            _picked?.Kill();
            EnsureGroup();
            _rect = (RectTransform)transform;
            _group.interactable = false;
        }

        private void OnDisable()
        {
            // 등장 도중에 꺼져도 다음에 보일 때 멀쩡하도록 끝 상태로 돌려 놓는다
            if (_entrance != null && _entrance.IsActive())
            {
                _entrance.Complete();
            }
        }

        public void Bind(int index, UpgradeChoice choice, Sprite icon, Sprite background, Action<int> onPick,
            Func<FormHint, Sprite> formIcon = null)
        {
            _index = index;
            _onPick = onPick;
            _background.sprite = background;
            _iconImage.sprite = icon;
            ShowFormHints(choice.FormHints, formIcon);


            if (choice.IsGeneral)
            {
                // 일반 카드는 아이콘이 없을 수 있다 (없으면 빈 흰 칸 대신 숨긴다)
                _iconImage.enabled = icon != null;
                _titleText.text = choice.DisplayName;
                _levelText.text = string.Empty;
                _descText.text = choice.DisplayDescription ?? string.Empty;
            }
            else if (choice.IsNewWeapon)
            {
                _iconImage.enabled = true;
                _titleText.text = choice.newSkillData.name;
                _levelText.text = "NEW";
                _descText.text = choice.newSkillData.desc ?? string.Empty;
            }
            else
            {
                _iconImage.enabled = true;
                _titleText.text = choice.DisplayName ?? choice.Option.name;
                _levelText.text = $"{choice.skill.Level} → {choice.skill.Level + 1}";
                _descText.text = choice.DisplayDescription ?? choice.Option.desc ?? string.Empty;
            }
        }

        // 이 카드가 조건이 되는 변환은 그대로, 이 카드를 고르면 막히는 변환은 X 표시로 보인다
        private void ShowFormHints(IReadOnlyList<FormHint> hints, Func<FormHint, Sprite> formIcon)
        {
            if (_formHints == null) { return; }
            int count = hints?.Count ?? 0;
            for (int i = 0; i < _formHints.Length; i++)
            {
                if (_formHints[i] == null) { continue; }
                if (i < count)
                {
                    var hint = hints[i];
                    _formHints[i].Show(formIcon?.Invoke(hint), hint.Kind == FormHintKind.Blocks);
                }
                else
                {
                    _formHints[i].Hide();
                }
            }
        }
    }
}
