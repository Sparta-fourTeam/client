using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.View
{
    /// <summary>보상 한 칸: 아이콘과 수량. ItemId는 아이템 ID 또는 RewardRows.CoinId/ExpId다(상세 창에 쓴다)</summary>
    public readonly struct ResultRewardItem
    {
        public Sprite Icon { get; }
        public int Count { get; }
        public string ItemId { get; }

        public ResultRewardItem(Sprite icon, int count, string itemId = null)
        {
            Icon = icon;
            Count = count;
            ItemId = itemId;
        }
    }

    /// <summary>"임무 보상" 영역. 보상 목록으로 Show를 호출한다. 목록이 비면 영역 전체가 꺼진다.
    /// 칸은 공용 아이템 칸(ItemCell 프리팹, ItemCellView)이다</summary>
    public sealed class ResultRewardListView : MonoBehaviour
    {
        [SerializeField] private ItemCellView[] _cells;

        /// <summary>지금 칸 전부 (보이지 않는 칸 포함)</summary>
        public IReadOnlyList<ItemCellView> Cells => _cells;

        /// <param name="onClick">칸을 누르면 그 칸의 순서로 부른다. null이면 칸이 눌리지 않는다</param>
        public void Show(IReadOnlyList<ResultRewardItem> items, Action<int> onClick = null)
        {
            EnsureCells(items.Count);
            for (int i = 0; i < _cells.Length; i++)
            {
                if (i >= items.Count)
                {
                    _cells[i].Hide();
                    continue;
                }

                int index = i;
                _cells[i].Show(items[i].Icon, items[i].ItemId == RewardRows.BonusExpId ? $"+{items[i].Count:N0}" : $"x{items[i].Count:N0}", onClick == null ? null : () => onClick(index));
            }

            gameObject.SetActive(items.Count > 0);
        }

        /// <summary>보상이 칸보다 많으면 마지막 칸을 복제해 늘린다. 항목이 조용히 잘리지 않게 한다</summary>
        private void EnsureCells(int count)
        {
            if (count <= _cells.Length || _cells.Length == 0)
            {
                return;
            }

            var template = _cells[_cells.Length - 1];
            var grown = new ItemCellView[count];
            Array.Copy(_cells, grown, _cells.Length);
            for (int i = _cells.Length; i < count; i++)
            {
                grown[i] = Instantiate(template, template.transform.parent);
                grown[i].name = $"Slot{i + 1}";
            }

            _cells = grown;
        }
    }
}
