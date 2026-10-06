using System.Linq;

namespace Game.Core
{
    /// <summary>UI가 표시하는 다섯 항목. 예상 최대치와 실제 지급 결과 모두에서 계산한다.</summary>
    public readonly struct StageRewardSummary
    {
        public int Coin { get; }
        public int Exp { get; }
        public long SkillMaterial { get; }
        public long EquipmentMaterial { get; }
        public long GemChest { get; }
        public StageRewardSummary(int coin, int exp, System.Collections.Generic.IEnumerable<ItemAmount> items)
        {
            Coin = coin; Exp = exp;
            var rows = items.ToList();
            SkillMaterial = rows.Where(item => item.itemId == ItemIds.RandomSkillMaterial || ItemIds.SkillMaterials.Contains(item.itemId)).Sum(item => (long)item.quantity);
            EquipmentMaterial = rows.Where(item => ItemIds.EquipmentMaterials.Contains(item.itemId)).Sum(item => (long)item.quantity);
            GemChest = rows.Where(item => item.itemId == ItemIds.GemChest).Sum(item => (long)item.quantity);
        }
    }
}
