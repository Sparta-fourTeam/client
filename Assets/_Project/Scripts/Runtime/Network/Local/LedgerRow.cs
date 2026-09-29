using System;

namespace Game.Network
{
    /// <summary>idempotencyKey로 중복 처리를 막는 재화 변동 기록</summary>
    [Serializable]
    public class LedgerRow
    {
        public string idempotencyKey;
        public int amount;
        public string sourceType;
        public string sourceId;
    }
}
