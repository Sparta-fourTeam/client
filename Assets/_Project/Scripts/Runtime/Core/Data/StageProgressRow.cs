using System;

namespace Game.Core
{
    /// <summary>스테이지 하나의 클리어 진행도</summary>
    [Serializable]
    public class StageProgressRow
    {
        public int stageId;

        /// <summary>0 = 클리어 안 됨, 1 = 클리어, 2 = 체력 50%로 클리어, 3 = 체력 100%로 클리어</summary>
        public int clearRating;

        /// <summary>지금까지 1회성 보상을 수령한 최고 rating. clearRating보다 낮으면 받을 보상이 남아있다</summary>
        public int claimedRating;

        public bool IsCleared => clearRating != 0;
        public bool HasUnclaimedReward => clearRating > claimedRating;
    }
}
