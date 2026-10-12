using System;
using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>인벤토리 격자의 칸 하나. 아이템이 있으면 아이콘과 보유 개수를 보여주고 누르면 상세를 연다.
    /// 빈칸이면 틀만 보이고 눌리지 않는다</summary>
    public sealed class InventorySlotView : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _countText;

        private Action _onClick;
        private bool _listening;

        public InventoryItem Item { get; private set; }

        /// <summary>아이템이 들어 있는 칸인가</summary>
        public bool IsFilled { get; private set; }

        public void Bind(InventoryItem item, Sprite icon, Action onClick)
        {
            // 복제한 칸은 Awake 순서와 상관없이 처음 묶을 때 한 번만 연결한다
            if (!_listening)
            {
                _listening = true;
                _button.onClick.AddListener(() => _onClick?.Invoke());
            }

            Item = item;
            IsFilled = true;
            _onClick = onClick;
            // 아이콘이 없으면 자리표시 이미지를 그대로 둔다
            if (icon != null)
            {
                _icon.sprite = icon;
            }

            _icon.enabled = true;
            _countText.text = CurrencyFormat.Format(item.Quantity);
            _button.interactable = true;
            gameObject.SetActive(true);
        }

        public void ShowEmpty()
        {
            Item = default;
            IsFilled = false;
            _onClick = null;
            _icon.enabled = false;
            _countText.text = string.Empty;
            _button.interactable = false;
            gameObject.SetActive(true);
        }
    }
}
