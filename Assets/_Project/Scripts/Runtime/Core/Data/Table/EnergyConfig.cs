using System;

namespace Game.Core
{
    /// <summary>에너지 최대치·자연 회복 주기·스테이지 입장 비용 설정</summary>
    [Serializable]
    public class EnergyConfig
    {
        public int Max;
        public float RegenSeconds;

        /// <summary>스테이지 입장에 드는 에너지. 모든 스테이지가 공통으로 쓴다</summary>
        public int Cost;

        /// <summary>광고 회복 1회에 주는 에너지</summary>
        public int AdRecoverAmount;

        /// <summary>유료 구매 회복 1회에 주는 에너지</summary>
        public int PurchaseRecoverAmount;
    }
}
