using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>플레이어 상태 전체 스냅샷. 증분이 아닌 최신 상태 전체로 덮어쓴다</summary>
    [Serializable]
    public class PlayerSnapshot
    {
        /// <summary>플레이어 닉네임. 계정을 만들 때 정해지고, 계정 삭제를 확인할 때 입력받는다</summary>
        public string nickname;
        public int gold;
        public int exp;
        public int energyStored;
        public string energyUpdatedAt;
        public List<StageProgressRow> stageProgress;
        public List<UpgradeRow> upgrades;
        /// <summary>스킬별·장비별 마법북 소유량. itemId당 한 행이며, 없는 아이템의 소유량은 0이다.</summary>
        public List<ItemAmount> items;
        /// <summary>도감에 등록된 몬스터. 전투에서 마주친 몬스터마다 한 행이며, 없는 몬스터는 아직 등록되지 않은 것이다.</summary>
        public List<CodexRow> codex;
        /// <summary>완료한 튜토리얼 ID (Tutorials.json)</summary>
        public List<string> tutorials;
    }
}
