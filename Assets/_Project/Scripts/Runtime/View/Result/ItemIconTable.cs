using System;
using UnityEngine;

namespace Game.View
{
    /// <summary>아이템 아이콘을 Items.json의 IconKey로 찾는 표. 코인과 EXP는 아이템이 아니라서 따로 둔다.
    /// 아이콘이 없는 키는 null을 돌려주고, 보여주는 쪽이 자리표시 이미지를 그대로 둔다</summary>
    [CreateAssetMenu(fileName = "ItemIconTable", menuName = "Project Nova/Item Icon Table")]
    public sealed class ItemIconTable : ScriptableObject
    {
        [Serializable]
        private sealed class Entry
        {
            public string iconKey;
            public Sprite icon;
        }

        [SerializeField] private Sprite _coin;
        [SerializeField] private Sprite _exp;
        [SerializeField] private Sprite _randomSkillMaterial;
        [SerializeField] private Sprite _randomEquipmentMaterial;
        [SerializeField] private Entry[] _items = Array.Empty<Entry>();

        public Sprite Coin => _coin;
        public Sprite Exp => _exp;

        /// <summary>일시정지 창의 "?" 랜덤 재료 아이콘. 결과 팝업에서는 이미 뽑힌 재료만 나오므로 쓰지 않는다</summary>
        public Sprite GetRandom(string itemId) =>
            itemId == Game.Core.ItemIds.RandomSkillMaterial ? _randomSkillMaterial
            : itemId == Game.Core.ItemIds.RandomEquipmentMaterial ? _randomEquipmentMaterial : null;

        public Sprite Get(string iconKey)
        {
            if (string.IsNullOrEmpty(iconKey))
            {
                return null;
            }

            foreach (var entry in _items)
            {
                if (entry.iconKey == iconKey)
                {
                    return entry.icon;
                }
            }

            return null;
        }
    }
}
