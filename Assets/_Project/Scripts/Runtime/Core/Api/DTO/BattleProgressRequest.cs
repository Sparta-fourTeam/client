using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>최초 전송 전에 실행 상태와 함께 저장하는 누적 보고. reportId와 본문은 재시도에서 바꾸지 않는다.</summary>
    [Serializable]
    public sealed class BattleProgressRequest
    {
        public string battleId;
        public string reportId;
        public string sourceRevision;
        public long expectedRevision;
        public int killedMonsters;
        public int completedWaves;
        public int reachedWave;
        public List<int> encounteredMonsterIds = new();
    }

    /// <summary>인정한 누적 기준과 최초 접수 기간. 현재 잔액이나 전투 결과 보상이 아니다.</summary>
    [Serializable]
    public sealed class BattleProgressAck
    {
        public string battleId;
        public int stageId;
        public int policyVersion;
        public string sourceRevision;
        public long revision;
        public int killedMonsters;
        public int completedWaves;
        public int reachedWave;
        public List<int> encounteredMonsterIds;
        public int acceptedDelta;
        public string acceptedAt;
        public string dailyKey;
        public string weeklyKey;
    }
}
