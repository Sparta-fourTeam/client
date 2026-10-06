using System;
using System.Collections.Generic;
using Game.Core;

namespace Game.Network
{
    /// <summary>지급 재시도에서 중복 및 요청 내용 변경을 판별하는 로컬 기록.</summary>
    [Serializable]
    public sealed class ItemGrantRow
    {
        public string idempotencyKey;
        public List<ItemAmount> items;
    }
}
