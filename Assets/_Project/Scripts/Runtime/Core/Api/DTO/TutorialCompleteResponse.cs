using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>튜토리얼 단계 완료 응답. 보상이 반영된 스냅샷과, 이번에 받은 아이템·코인(이미 완료했으면 빈 목록·0)</summary>
    [Serializable]
    public class TutorialCompleteResponse
    {
        public PlayerSnapshot snapshot;
        public List<ItemAmount> rewardItems;
        public int rewardGold;
    }
}
