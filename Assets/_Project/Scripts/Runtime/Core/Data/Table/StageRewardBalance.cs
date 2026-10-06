using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    [Serializable]
    public sealed class StageRewardBalance
    {
        public int StageId;
        public int BaseExp, ThreeStarBonusExp;
        /// <summary>시작·일반 누적·W3 보너스에 쓰는 재료 ID. 랜덤도 같은 ID 필드로 표현한다.</summary>
        public string SkillMaterial = ItemIds.RandomSkillMaterial;
        public int Wave3SkillMaterialBonusAmount;
        public List<ItemAmount> Wave4EquipmentMaterials = new();
        public int Wave5GemChestCount, Wave6CoinBonus;

        public void Validate()
        {
            if (StageId <= 0 || BaseExp < 0 || ThreeStarBonusExp < 0 || Wave3SkillMaterialBonusAmount < 0
                || Wave5GemChestCount < 0 || Wave6CoinBonus < 0
                || (SkillMaterial != ItemIds.RandomSkillMaterial && !ItemIds.SkillMaterials.Contains(SkillMaterial)))
            { throw new InvalidOperationException($"StageRewards {StageId}: 보상 데이터가 잘못되었습니다."); }
            if (Wave4EquipmentMaterials == null) { throw new InvalidOperationException("장비재료 목록이 필요합니다."); }
            var ids = new HashSet<string>();
            foreach (var item in Wave4EquipmentMaterials)
            {
                if (item == null || item.quantity < 0 || !ItemIds.EquipmentMaterials.Contains(item.itemId) || !ids.Add(item.itemId))
                { throw new InvalidOperationException("장비재료는 종류별 비음수 수량으로 설정합니다."); }
            }
            if (ids.Count != ItemIds.EquipmentMaterials.Count)
            { throw new InvalidOperationException("모든 장비재료 종류의 수량을 설정해야 합니다."); }
        }
    }
}
