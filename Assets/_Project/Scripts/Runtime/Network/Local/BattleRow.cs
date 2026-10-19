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

        public long progressRevision;
        public int reportedKills;
        public int reportedCompletedWaves, reportedReachedWave;
        public List<int> reportedEncounters = new();
        public List<LocalProgressReceipt> progressReceipts = new();

        public int rewardGold;
        public int rewardExp, rewardBonusExp, clearRating;
        public List<ItemAmount> rewardItems = new();
    }

    [Serializable]
    public sealed class LocalProgressReceipt
    {
        public BattleProgressRequest request;
        public BattleProgressAck ack;
    }
}
