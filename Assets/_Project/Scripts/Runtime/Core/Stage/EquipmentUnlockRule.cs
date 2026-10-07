using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    /// <summary>열린 장비 판정. 장비는 플레이어 레벨이 Upgrades 테이블의 UnlockLevel 이상이면 열린다.
    /// 랜덤 장비재료는 열린 장비의 재료 중에서만 뽑는다</summary>
    public static class EquipmentUnlockRule
    {
        /// <summary>누적 경험치 exp의 플레이어 레벨에서 열려 있는 장비의 TargetId(예: equipment.hat) 목록</summary>
        public static List<string> UnlockedTargetIds(GameDataStore data, int exp)
        {
            int level = data.PlayerLevels.At(exp).level;
            return ItemIds.EquipmentMaterials
                .Select(materialId => data.Items.GetOrThrow(materialId).TargetId)
                .Where(targetId => data.Upgrades.GetOrThrow(targetId).UnlockLevel <= level)
                .ToList();
        }
    }
}
