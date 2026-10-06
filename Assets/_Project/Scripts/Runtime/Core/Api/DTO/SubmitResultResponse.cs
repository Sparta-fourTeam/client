using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>전투 결과 제출 응답</summary>
    [Serializable]
    public class SubmitResultResponse
    {
        public bool cleared;

        /// <summary>서버가 검증 후 확정한 보상 골드. 재전송돼도 중복 지급되지 않는다</summary>
        public int rewardGold;

        /// <summary>전투 결과 처리에서 확정·지급한 마법북. 재제출에도 처음 확정된 보상을 반환한다.</summary>
        public List<ItemAmount> rewardItems = new();
    }
}
