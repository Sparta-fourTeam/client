using Game.Core;

namespace Game.Network
{
    /// <summary>마법북 소모. 장비·스킬 강화 같은 구매 API가 공통으로 쓴다. 저장은 호출한 API가 골드 차감과 함께 한 번 한다</summary>
    public static class LocalItemSpend
    {
        public static int Owned(LocalSave save, string itemId) =>
            save.items?.Find(item => item.itemId == itemId)?.quantity ?? 0;

        /// <summary>소지량이 모자라면 INSUFFICIENT_ITEM으로 거절하고 아무것도 바꾸지 않는다. 0이 된 행은 지운다</summary>
        public static void Spend(LocalSave save, string itemId, int quantity)
        {
            if (string.IsNullOrWhiteSpace(itemId) || quantity <= 0)
            {
                throw new ApiException(ApiErrorKind.Rejected, "INVALID_ITEM_COST");
            }

            int owned = Owned(save, itemId);
            if (owned < quantity)
            {
                throw new ApiException(ApiErrorKind.Rejected, "INSUFFICIENT_ITEM");
            }

            var row = save.items.Find(item => item.itemId == itemId);
            row.quantity = owned - quantity;
            if (row.quantity == 0)
            {
                save.items.Remove(row);
            }
        }
    }
}
