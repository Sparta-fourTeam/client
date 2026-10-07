using System;
using System.Collections.Generic;
using Game.Core;

namespace Game.Network
{
    /// <summary>발급된 전투 한 건. status로 중복 결과 제출을 막는다</summary>
    [Serializable]
    public class BattleRow
    {
        public string battleKey;
        public int stageId;
        public int seed;

        /// <summary>Issued -> Cleared 또는 Failed</summary>
        public string status;

        public int rewardGold;
        public int rewardExp, clearRating;
        /// <summary>0은 기존 클리어 보상 전투, 1은 20웨이브 누적 보상 전투.</summary>
        public int rewardRuleVersion;
        public List<ItemAmount> rewardItems = new();
    }
}
