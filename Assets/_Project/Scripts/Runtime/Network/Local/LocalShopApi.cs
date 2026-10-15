using System;
using Cysharp.Threading.Tasks;
using Game.Core;

namespace Game.Network
{
    /// <summary>일일상점 다시 뽑기와 구매를 로컬 저장소에 반영하는 Mock 구현. idempotencyKey로 연타를 막는다. 거절되면 아무것도 바뀌지 않는다</summary>
    public sealed class LocalShopApi : IShopApi
    {
        private readonly LocalSaveStore _store;
        private readonly GameDataStore _data;

        public LocalShopApi(LocalSaveStore store, GameDataStore data)
        {
            _store = store;
            _data = data;
        }

        public UniTask<PlayerSnapshot> Reroll(ShopRerollKind kind, string idempotencyKey)
        {
            var save = _store.Load();
            if (kind == ShopRerollKind.Gold && save.ledger.Exists(l => l.idempotencyKey == idempotencyKey))
            {
                return UniTask.FromResult(LocalPlayerApi.ToSnapshot(save));
            }

            LocalShop.Reroll(save, _data, kind, idempotencyKey);
            _store.Flush(save);
            return UniTask.FromResult(LocalPlayerApi.ToSnapshot(save));
        }

        public UniTask<PlayerSnapshot> Buy(int slot, string idempotencyKey)
        {
            var save = _store.Load();
            if (save.ledger.Exists(l => l.idempotencyKey == idempotencyKey))
            {
                return UniTask.FromResult(LocalPlayerApi.ToSnapshot(save));
            }

            LocalShop.Buy(save, _data, slot, idempotencyKey, DateTime.UtcNow);
            _store.Flush(save);
            return UniTask.FromResult(LocalPlayerApi.ToSnapshot(save));
        }

        public UniTask<PlayerSnapshot> BuyGoldPack(int index, string idempotencyKey)
        {
            var save = _store.Load();
            if (save.ledger.Exists(l => l.idempotencyKey == idempotencyKey))
            {
                return UniTask.FromResult(LocalPlayerApi.ToSnapshot(save));
            }

            LocalShop.BuyGoldPack(save, _data, index, idempotencyKey);
            _store.Flush(save);
            return UniTask.FromResult(LocalPlayerApi.ToSnapshot(save));
        }

        public UniTask<PlayerSnapshot> BuyGemPack(int index, string idempotencyKey)
        {
            var save = _store.Load();
            if (save.ledger.Exists(l => l.idempotencyKey == idempotencyKey))
            {
                return UniTask.FromResult(LocalPlayerApi.ToSnapshot(save));
            }

            LocalShop.BuyGemPack(save, _data, index, idempotencyKey);
            _store.Flush(save);
            return UniTask.FromResult(LocalPlayerApi.ToSnapshot(save));
        }
    }
}
