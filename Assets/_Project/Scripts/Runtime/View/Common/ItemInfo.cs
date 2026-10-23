using Game.Core;

namespace Game.View
{
    /// <summary>상세 창에 보여줄 아이템·재화의 이름과 설명. 이름은 Items 표, 코인·EXP·랜덤 재료처럼 표에 없는 것은 여기서 정한다</summary>
    public static class ItemInfo
    {
        public static string NameOf(string itemId, GameDataStore data)
        {
            switch (itemId)
            {
                case RewardRows.CoinId: return L10n.Get("currency.ink");
                case RewardRows.GemId: return L10n.Get("currency.gem");
                case RewardRows.ExpId: return L10n.Get("item.exp");
                case RewardRows.BonusExpId: return L10n.Get("item.bonusExp");
                case ItemIds.RandomSkillMaterial: return L10n.Get("item.randomSkillBook");
                case ItemIds.RandomEquipmentMaterial: return L10n.Get("item.randomEnchantScroll");
            }

            return data != null && itemId != null && data.Items.Contains(itemId) ? data.Items.GetOrThrow(itemId).Name : itemId ?? string.Empty;
        }

        /// <summary>추첨 전 보상 미리보기의 이름. 상점·인벤토리의 실제 아이템 이름과 구분한다.</summary>
        public static string RewardNameOf(string itemId, GameDataStore data) =>
            RandomRewardOdds.IsRandom(itemId, data)
                ? data.RewardRules.Items[itemId].Kind == "SkillMaterial" ? L10n.Get("item.randomSkillBook") : L10n.Get("item.randomEnchantScroll")
                : NameOf(itemId, data);

        public static string DescriptionOf(string itemId, GameDataStore data = null)
        {
            if (RandomRewardOdds.IsRandom(itemId, data))
            {
                return data.RewardRules.Items[itemId].Kind == "SkillMaterial"
                    ? L10n.Get("itemDesc.randomSkillBook") : L10n.Get("itemDesc.randomEnchantScroll");
            }
            switch (itemId)
            {
                case RewardRows.CoinId: return L10n.Get("itemDesc.ink");
                case RewardRows.GemId: return L10n.Get("itemDesc.gem");
                case ShopRules.EnergyReward: return L10n.Get("itemDesc.energy");
                case RewardRows.ExpId: return L10n.Get("itemDesc.exp");
                case RewardRows.BonusExpId: return L10n.Get("itemDesc.bonusExp");
                case ItemIds.RandomSkillMaterial: return L10n.Get("itemDesc.randomSkillBook");
                case ItemIds.RandomEquipmentMaterial: return L10n.Get("itemDesc.randomEnchantScroll");
                case ItemIds.GemChest: return L10n.Get("itemDesc.gemChest");
            }

            return InventoryItems.CategoryOf(itemId) switch
            {
                InventoryCategory.SkillMaterial => L10n.Get("itemDesc.skillMaterial"),
                InventoryCategory.EquipmentMaterial => L10n.Get("itemDesc.equipMaterial"),
                _ => L10n.Get("itemDesc.other"),
            };
        }
    }
}
