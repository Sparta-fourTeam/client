using System;
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


        private void Awake()
        {
            _button.onClick.AddListener(() => _onPick?.Invoke(_index));
        }

        public void Bind(int index, UpgradeChoice choice, Sprite icon, Sprite background, Action<int> onPick)
        {
            _index = index;
            _onPick = onPick;
            _background.sprite = background;
            _iconImage.sprite = icon;


            if (choice.IsNewWeapon)
            {
                _titleText.text = choice.NewWeaponData.name;
                _levelText.text = "NEW";
                _descText.text = choice.NewWeaponData.desc ?? string.Empty;
            }
            else
            {
                _titleText.text = choice.DisplayName ?? choice.Option.name;
                _levelText.text = $"{choice.Weapon.Level} » {choice.Weapon.Level + 1}";
                _descText.text = choice.DisplayDescription ?? choice.Option.desc ?? string.Empty;
            }
        }
    }
}
