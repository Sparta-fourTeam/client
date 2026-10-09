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


        private const float EntranceSeconds = 0.28f;
        private const float EntranceStartScale = 0.7f;
        private const float PickedSeconds = 0.18f;
        private const float PickedPunch = 0.12f;
        private const float DismissedScale = 0.85f;

        private Sequence _entrance;
        private CanvasGroup _group;
        private RectTransform _rect;

        private void Awake()
        {
            _button.onClick.AddListener(() => _onPick?.Invoke(_index));
        }

        /// <summary>카드가 작게 시작해 커지며 나타난다. delay만큼 기다렸다 시작하므로 카드마다 다르게 주면 차례로 나온다.
        /// 등장하는 동안에는 눌리지 않는다. 일시정지 중에도 흐르도록 unscaled 시간을 쓴다</summary>
        public void PlayEntrance(float delay)
        {
            _entrance?.Kill();
            EnsureGroup();
            _rect = (RectTransform)transform;

            // 위치는 HorizontalLayoutGroup이 정하므로 건드리지 않고 크기와 투명도만 쓴다
            _group.alpha = 0f;
            _group.interactable = false;
            _rect.localScale = Vector3.one * EntranceStartScale;

            _entrance = DOTween.Sequence().SetDelay(delay).SetUpdate(true).SetLink(gameObject);
            _entrance.Join(DOTween.To(() => _group.alpha, a => _group.alpha = a, 1f, EntranceSeconds * 0.6f));
            _entrance.Join(_rect.DOScale(1f, EntranceSeconds).SetEase(Ease.OutBack));
            _entrance.OnComplete(() => _group.interactable = true);
        }

        /// <summary>이 카드가 골라졌다. 한 번 부풀었다 돌아오며 눌러졌음을 알린다</summary>
        public void PlayPicked()
        {
            Prepare();
            _rect.localScale = Vector3.one;
            _rect.DOPunchScale(Vector3.one * PickedPunch, PickedSeconds, 1, 0f).SetUpdate(true).SetLink(gameObject);
        }

        /// <summary>다른 카드가 골라졌다. 작아지며 사라진다</summary>
        public void PlayDismissed()
        {
            Prepare();
            _group.interactable = false;
            DOTween.To(() => _group.alpha, a => _group.alpha = a, 0f, PickedSeconds).SetUpdate(true).SetLink(gameObject);
            _rect.DOScale(DismissedScale, PickedSeconds).SetEase(Ease.InCubic).SetUpdate(true).SetLink(gameObject);
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
