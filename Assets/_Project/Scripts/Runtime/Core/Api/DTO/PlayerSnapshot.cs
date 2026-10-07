using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>플레이어 상태 전체 스냅샷. 증분이 아닌 최신 상태 전체로 덮어쓴다</summary>
    [Serializable]
    public class PlayerSnapshot
    {
        public int gold;
        public int exp;
        public int energyStored;
        public string energyUpdatedAt;
        public List<StageProgressRow> stageProgress;
        public List<UpgradeRow> upgrades;
        /// <summary>장비별 강화 레벨. 강화한 적이 없는 장비는 행이 없다(레벨 0)</summary>
        public List<EquipmentRow> equipments;
        /// <summary>스킬별·장비별 마법북 소유량. itemId당 한 행이며, 없는 아이템의 소유량은 0이다.</summary>
        public List<ItemAmount> items;
    }
}
