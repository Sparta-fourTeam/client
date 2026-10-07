using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>"등장 요마" 칸 하나: 아이콘과 보스 표시</summary>
    public readonly struct StageMonsterItem
    {
        public UnityEngine.Sprite Icon { get; }
        public string Name { get; }
        public bool IsBoss { get; }

        public StageMonsterItem(UnityEngine.Sprite icon, string name, bool isBoss)
        {
            Icon = icon;
            Name = name;
            IsBoss = isBoss;
        }
    }

    /// <summary>"등장 요마" 영역. 목록으로 Show를 호출한다. 호출할 때마다 칸을 전부 다시 채워 이전 스테이지 정보가 남지 않고, 목록이 비면 영역 전체가 꺼진다</summary>
    public sealed class StageMonsterListView : MonoBehaviour
    {
        [Serializable]
        private sealed class Slot
        {
            public GameObject Root;
            public Image Icon;
            public GameObject BossBadge;
        }

        [SerializeField] private Slot[] _slots;

        public void Show(IReadOnlyList<StageMonsterItem> items)
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

                _slots[i].BossBadge.SetActive(items[i].IsBoss);
            }

            gameObject.SetActive(items.Count > 0);
        }

        /// <summary>몬스터가 슬롯보다 많으면 마지막 슬롯을 복제해 늘린다. 항목이 조용히 잘리지 않게 한다</summary>
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
                    BossBadge = root.transform.Find("BossBadge").gameObject
                };
            }

            _slots = grown;
        }
    }
}
