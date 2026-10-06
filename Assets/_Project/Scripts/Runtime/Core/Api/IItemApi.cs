using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Game.Core
{
    /// <summary>마법북 지급을 반영하고 최신 소유량 스냅샷을 돌려준다.</summary>
    public interface IItemApi
    {
        /// <summary>한 지급 건의 모든 아이템을 함께 반영한다. 재시도는 같은 키와 같은 수량을 사용한다.</summary>
        UniTask<PlayerSnapshot> Grant(IReadOnlyList<ItemAmount> items, string idempotencyKey);
    }
}
