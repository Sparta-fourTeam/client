using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>보상 한 칸: 아이콘과 수량</summary>
    public readonly struct ResultRewardItem
    {
        public Sprite Icon { get; }
        public int Count { get; }

        public ResultRewardItem(Sprite icon, int count)
        {
            Icon = icon;
            Count = count;
        }
    }

    /// <summary>"임무 보상" 영역. 보상 목록으로 Show를 호출한다. 목록이 비면 영역 전체가 꺼진다</summary>
    public sealed class ResultRewardListView : MonoBehaviour
    {
        [Serializable]
        private sealed class Slot
        {
            public GameObject Root;
            public Image Icon;
            public TMP_Text Count;
        }

        [SerializeField] private Slot[] _slots;

        public void Show(IReadOnlyList<ResultRewardItem> items)
        {
            EnsureSlots(items.Count);
            for (int i = 0; i < _slots.Length; i++)
            {
                bool has = i < items.Count;
                _slots[i].Root.SetActive(has);
                if (!has)
                {
                    continue;
                }

                // 아이콘이 없으면 프리팹의 자리표시 스프라이트를 그대로 둔다
                if (items[i].Icon != null)
                {
                    _slots[i].Icon.sprite = items[i].Icon;
                }

                _slots[i].Count.text = $"x{items[i].Count:N0}";
            }

            gameObject.SetActive(items.Count > 0);
        }

        /// <summary>보상이 슬롯보다 많으면 마지막 슬롯을 복제해 늘린다. 항목이 조용히 잘리지 않게 한다</summary>
        private void EnsureSlots(int count)
        {
            if (count <= _slots.Length || _slots.Length == 0)
            {
                return;
            }

            var template = _slots[_slots.Length - 1].Root;
            var grown = new Slot[count];
            Array.Copy(_slots, grown, _slots.Length);
            for (int i = _slots.Length; i < count; i++)
            {
                var root = Instantiate(template, template.transform.parent);
                root.name = $"Slot{i + 1}";
                grown[i] = new Slot
                {
                    Root = root,
                    Icon = root.transform.Find("Icon").GetComponent<Image>(),
                    Count = root.transform.Find("Count").GetComponent<TMP_Text>()
                };
            }

            _slots = grown;
        }
    }
}
