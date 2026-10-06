using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Game.Core;

namespace Game.Network
{
    /// <summary>검증된 마법북 지급을 저장하고 요청 키별로 한 번만 반영하는 Local 구현.</summary>
    public sealed class LocalItemApi : IItemApi
    {
        private readonly LocalSaveStore _store;
        private readonly GameDataStore _data;

        public LocalItemApi(LocalSaveStore store, GameDataStore data)
        {
            _store = store;
            _data = data;
        }

        public UniTask<PlayerSnapshot> Grant(IReadOnlyList<ItemAmount> items, string idempotencyKey)
        {
            if (string.IsNullOrWhiteSpace(idempotencyKey) || items == null || items.Count == 0)
            {
                throw new ApiException(ApiErrorKind.Rejected, "INVALID_REQUEST");
            }

            var quantities = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var item in items)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.itemId) || item.quantity <= 0)
                {
                    throw new ApiException(ApiErrorKind.Rejected, "INVALID_REQUEST");
                }
                _data.Items.GetOrThrow(item.itemId);
                quantities.TryGetValue(item.itemId, out var current);
                quantities[item.itemId] = AddQuantity(current, item.quantity);
            }
            var normalized = quantities.OrderBy(item => item.Key, StringComparer.Ordinal)
                .Select(item => new ItemAmount { itemId = item.Key, quantity = item.Value }).ToList();

            var save = _store.Load();
            save.items ??= new();
            save.itemGrants ??= new();
            var previous = save.itemGrants.Find(row => row.idempotencyKey == idempotencyKey);
            if (previous != null)
            {
                if (previous.items == null || previous.items.Count != normalized.Count
                    || !previous.items.Zip(normalized, (left, right) =>
                        left.itemId == right.itemId && left.quantity == right.quantity).All(equal => equal))
                {
                    throw new ApiException(ApiErrorKind.Rejected, "IDEMPOTENCY_CONFLICT");
                }
                return UniTask.FromResult(LocalPlayerApi.ToSnapshot(save));
            }

            foreach (var item in normalized)
            {
                var owned = save.items.Find(row => row.itemId == item.itemId);
                if (owned == null)
                {
                    save.items.Add(new ItemAmount { itemId = item.itemId, quantity = item.quantity });
                }
                else
                {
                    owned.quantity = AddQuantity(owned.quantity, item.quantity);
                }
            }
            // 목록 전체가 성공한 뒤 지급 기록과 소유량을 같은 세이브에 한 번 저장한다.
            save.itemGrants.Add(new ItemGrantRow { idempotencyKey = idempotencyKey, items = normalized });
            _store.Flush(save);
            return UniTask.FromResult(LocalPlayerApi.ToSnapshot(save));
        }

        private static int AddQuantity(int current, int added)
        {
            try { return checked(current + added); }
            catch (OverflowException)
            {
                throw new ApiException(ApiErrorKind.Rejected, "ITEM_QUANTITY_OVERFLOW");
            }
        }
    }
}
