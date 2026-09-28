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
    }
}
