using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>플레이어 상태 전체 스냅샷. 증분이 아닌 최신 상태 전체로 덮어쓴다</summary>
    [Serializable]
    public class PlayerSnapshot
    {
        public int gold;
        public int energyStored;
        public string energyUpdatedAt;
        public List<StageProgressRow> stageProgress;
        public List<UpgradeRow> upgrades;
    }
}
