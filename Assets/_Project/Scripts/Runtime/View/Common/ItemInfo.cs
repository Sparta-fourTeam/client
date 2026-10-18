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
                case RewardRows.CoinId: return "잉크";
                case RewardRows.GemId: return "잼";
                case RewardRows.ExpId: return "경험치";
                case RewardRows.BonusExpId: return "3성 보너스 경험치";
                case ItemIds.RandomSkillMaterial: return "랜덤 마법북";
                case ItemIds.RandomEquipmentMaterial: return "랜덤 인챈트 주문서";
            }

            return data != null && itemId != null && data.Items.Contains(itemId) ? data.Items.GetOrThrow(itemId).Name : itemId ?? string.Empty;
        }

        public static string DescriptionOf(string itemId)
        {
            switch (itemId)
            {
                case RewardRows.CoinId: return "스킬과 장비를 강화할 때 쓰는 재화예요.";
                case RewardRows.GemId: return "상점에서 물품을 사는 데 쓰는 재화예요.";
                case ShopRules.EnergyReward: return "스테이지에 입장할 때 쓰는 잉크펜이에요.";
                case RewardRows.ExpId: return "모으면 플레이어 레벨이 올라요.\n레벨이 오르면 새 스킬과 장비가 열려요.";
                case RewardRows.BonusExpId: return "3성으로 클리어해서 추가로 받은 경험치예요.\n지급 경험치 합계에 이미 포함되어 있어요.";
                case ItemIds.RandomSkillMaterial: return "열린 스킬 중 하나의 마법북을 받아요.";
                case ItemIds.RandomEquipmentMaterial: return "열린 장비 중 하나의 인챈트 주문서를 받아요.";
                case ItemIds.GemChest: return "보석이 들어 있는 상자예요.";
            }

            return InventoryItems.CategoryOf(itemId) switch
            {
                InventoryCategory.SkillMaterial => "스킬을 강화할 때 쓰는 마법북이에요.\n스킬 화면에서 사용해요.",
                InventoryCategory.EquipmentMaterial => "장비를 강화할 때 쓰는 주문서예요.\n캐릭터 화면에서 사용해요.",
                _ => "보관 중인 아이템이에요.",
            };
        }
    }
}
