using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>전투 결과 제출 요청. 재시도 시에도 처음 만든 인스턴스를 그대로 재사용해야 한다 (playTime이 달라지면 다른 요청으로 취급됨)</summary>
    [Serializable]
    public class SubmitResultRequest
    {
        public string battleId;
        public bool cleared;
        public int reachedWave, completedWaves, kills;
        public float playTime;
        public List<string> buildLog;
        public string createdAt;
    }
}
