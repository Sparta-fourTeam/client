using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>전투 결과 제출 요청. battleId가 중복 제출 판단 기준이므로, 재시도 시에도 처음 만든 인스턴스를 그대로 재사용해야 한다</summary>
    [Serializable]
    public class SubmitResultRequest
    {
        public string battleId;
        public bool cleared;
        public int reachedWave, kills;
        public float playTime;
        public List<string> buildLog;
        public string createdAt;

        /// <summary>클리어 시점의 벽 잔여 체력 비율(0~100). clearRating(1~3) 판정에 쓰인다</summary>
        public int wallHpPercent;
    }
}
