using System;

namespace Game.Network
{
    /// <summary>재화·에너지 상태. LocalSave 전용 — 서버 전환 후에는 서버가 이 역할을 대신한다</summary>
    [Serializable]
    public class WalletRow
    {
        public int gold;
        public int energyStored;

        /// <summary>에너지가 마지막으로 갱신된 시각. 경과 시간만큼 자연 회복량을 계산하는 데 쓰인다</summary>
        public string energyUpdatedAt;
    }
}
