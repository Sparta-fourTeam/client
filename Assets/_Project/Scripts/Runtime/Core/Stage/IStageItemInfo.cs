using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>현재 스테이지의 획득 가능 마법북과 실제 획득량을 구분하는 조회 계약.</summary>
    public interface IStageItemInfo
    {
        int StageId { get; }
        IReadOnlyList<string> ObtainableItemIds { get; }
        IReadOnlyList<ItemAmount> AcquiredItems { get; }
        int AcquiredQuantity(string itemId);
    }
}
