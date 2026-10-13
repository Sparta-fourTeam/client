using System;
using System.Collections.Generic;

namespace Game.Core
{
    public enum InventoryCategory
    {
        All,
        /// <summary>스킬 마법북</summary>
        SkillMaterial,
        /// <summary>장비 인챈트 주문서</summary>
        EquipmentMaterial,
        /// <summary>그 밖의 아이템 (보석상자 등)</summary>
        Etc,
    }

    /// <summary>인벤토리 한 칸. 보유 개수가 1 이상인 아이템만 만든다</summary>
    public readonly struct InventoryItem
    {
        public string ItemId { get; }
        public string Name { get; }
        public int Quantity { get; }
        public InventoryCategory Category { get; }

        public InventoryItem(string itemId, string name, int quantity, InventoryCategory category)
        {
            ItemId = itemId;
            Name = name;
            Quantity = quantity;
            Category = category;
        }
    }

    /// <summary>Items 표와 보유 개수로 인벤토리 칸 목록을 만든다. 순서는 스킬 마법북, 장비 주문서, 그 밖의 아이템이고
    /// 같은 분류 안에서는 ItemIds에 적힌 순서(그 밖은 ID 순)다</summary>
    public static class InventoryItems
    {
        public static InventoryCategory CategoryOf(string itemId)
        {
            if (Index(ItemIds.SkillMaterials, itemId) >= 0) { return InventoryCategory.SkillMaterial; }
            if (Index(ItemIds.EquipmentMaterials, itemId) >= 0) { return InventoryCategory.EquipmentMaterial; }
            return InventoryCategory.Etc;
        }

        /// <param name="quantityOf">아이템 ID의 보유 개수 (보통 PlayerProfile.ItemQuantity)</param>
        public static List<InventoryItem> Build(IEnumerable<ItemDefinition> definitions, Func<string, int> quantityOf, InventoryCategory filter)
        {
            var items = new List<InventoryItem>();
            foreach (var definition in definitions)
            {
                int quantity = quantityOf(definition.Id);
                var category = CategoryOf(definition.Id);
                if (quantity <= 0 || (filter != InventoryCategory.All && filter != category))
                {
                    continue;
                }

                items.Add(new InventoryItem(definition.Id, definition.Name, quantity, category));
            }

            items.Sort((a, b) =>
            {
                int compare = a.Category.CompareTo(b.Category);
                if (compare == 0) { compare = OrderInCategory(a).CompareTo(OrderInCategory(b)); }
                if (compare == 0) { compare = string.CompareOrdinal(a.ItemId, b.ItemId); }
                return compare;
            });
            return items;
        }

        private static int OrderInCategory(InventoryItem item) => item.Category switch
        {
            InventoryCategory.SkillMaterial => Index(ItemIds.SkillMaterials, item.ItemId),
            InventoryCategory.EquipmentMaterial => Index(ItemIds.EquipmentMaterials, item.ItemId),
            _ => 0,
        };

        private static int Index(IReadOnlyList<string> list, string id)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == id) { return i; }
            }

            return -1;
        }
    }
}
