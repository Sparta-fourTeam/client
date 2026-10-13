using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;

namespace Game.Network
{
    /// <summary>보상 API 내부의 마법북 검증·합산. 저장과 중복 수령 판단은 호출한 보상 API가 담당한다.</summary>
    internal static class LocalItemRewards
    {
        public static List<ItemAmount> Apply(LocalSave save, IEnumerable<ItemAmount> rewards, GameDataStore data)
        {
            var quantities = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var item in rewards ?? Array.Empty<ItemAmount>())
            {
                if (item == null || string.IsNullOrWhiteSpace(item.itemId) || item.quantity <= 0)
                {
                    throw new ApiException(ApiErrorKind.Rejected, "INVALID_ITEM_REWARD");
                }
                data.Items.GetOrThrow(item.itemId);
                quantities.TryGetValue(item.itemId, out var current);
                quantities[item.itemId] = Add(current, item.quantity);
            }

            var owned = (save.items ?? new()).Select(item => new ItemAmount
            { itemId = item.itemId, quantity = item.quantity }).ToList();
            foreach (var item in quantities)
            {
                var row = owned.Find(entry => entry.itemId == item.Key);
                if (row == null) { owned.Add(new ItemAmount { itemId = item.Key, quantity = item.Value }); }
                else { row.quantity = Add(row.quantity, item.Value); }
            }

            // 전체 계산이 성공한 뒤 교체한다. 호출자는 골드·수령 상태와 함께 한 번 저장한다.
            save.items = owned;
            return quantities.OrderBy(item => item.Key, StringComparer.Ordinal)
                .Select(item => new ItemAmount { itemId = item.Key, quantity = item.Value }).ToList();
        }

        private static int Add(int current, int added)
        {
            try { return checked(current + added); }
            catch (OverflowException) { throw new ApiException(ApiErrorKind.Rejected, "ITEM_QUANTITY_OVERFLOW"); }
        }
    }
}
