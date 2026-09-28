using System;

namespace Game.Core
{
    /// <summary>스테이지 테이블 한 행</summary>
    [Serializable]
    public class StageDefinition
    {
        public int Id;
        public int ClearGold;

        /// <summary>rating 1~3 클리어 시 1회성 보상 (index 0 = rating 1)</summary>
        public int[] RatingRewards;
    }
}
