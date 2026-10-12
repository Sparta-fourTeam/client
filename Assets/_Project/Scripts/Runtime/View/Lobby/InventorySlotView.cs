using System;
using Game.Core;
using UnityEngine;

namespace Game.View
{
    /// <summary>인벤토리 격자의 칸 하나. 공용 아이템 칸(ItemCellView)에 아이템을 그리고, 어떤 아이템을 담았는지 기억한다.
    /// 아이템이 있으면 누르면 상세를 열고, 빈칸이면 틀만 보이고 눌리지 않는다</summary>
    public sealed class InventorySlotView : MonoBehaviour
    {
        [SerializeField] private ItemCellView _cell;

        public InventoryItem Item { get; private set; }

        /// <summary>아이템이 들어 있는 칸인가</summary>
        public bool IsFilled { get; private set; }

        public ItemCellView Cell => _cell;

        public void Bind(InventoryItem item, Sprite icon, Action onClick)
        {
            Item = item;
            IsFilled = true;
            _cell.Show(icon, CurrencyFormat.Format(item.Quantity), onClick);
        }

        public void ShowEmpty()
        {
            Item = default;
            IsFilled = false;
            _cell.ShowEmpty();
        }
    }
}
