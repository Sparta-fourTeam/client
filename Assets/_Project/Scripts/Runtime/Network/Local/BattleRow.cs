using System;

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
    }
}
