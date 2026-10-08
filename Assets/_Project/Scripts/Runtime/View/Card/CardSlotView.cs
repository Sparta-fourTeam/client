using System;
using System.Collections.Generic;
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


        private void Awake()
        {
            _button.onClick.AddListener(() => _onPick?.Invoke(_index));
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
                _levelText.text = $"{choice.skill.Level} » {choice.skill.Level + 1}";
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
