using Cysharp.Threading.Tasks;
using Game.Core;

namespace Game.Network
{
    /// <summary>업그레이드 구매를 로컬 저장소에 반영하는 Mock 구현. idempotencyKey로 연타를 막는다</summary>
    public sealed class LocalUpgradeApi : IUpgradeApi
    {
        private readonly LocalSaveStore _store;
        private readonly GameDataStore _data;

        public LocalUpgradeApi(LocalSaveStore store, GameDataStore data)
        {
            _store = store;
            _data = data;
        }

        public UniTask<PlayerSnapshot> Purchase(string upgradeId, string idempotencyKey)
        {
            var save = _store.Load();
            if (save.ledger.Exists(l => l.idempotencyKey == idempotencyKey))
            {
                return UniTask.FromResult(LocalPlayerApi.ToSnapshot(save));
            }

            var def = _data.Upgrades.GetOrThrow(upgradeId);
            var row = save.upgrades.Find(u => u.upgradeId == upgradeId);
            int level = row?.level ?? 0;
            if (level >= def.MaxLevel)
            {
                throw new ApiException(ApiErrorKind.Rejected, "ALREADY_MAX");
            }

            int cost = def.CostAt(level + 1);
            if (save.wallet.gold < cost)
            {
                throw new ApiException(ApiErrorKind.Rejected, "INSUFFICIENT_GOLD");
            }

            save.wallet.gold -= cost;
            save.ledger.Add(new LedgerRow { idempotencyKey = idempotencyKey, amount = -cost, sourceType = "UPGRADE", sourceId = upgradeId });
            if (row == null)
            {
                save.upgrades.Add(new UpgradeRow { upgradeId = upgradeId, level = level + 1 });
            }
            else
            {
                row.level = level + 1;
            }

            _store.Flush(save);
            return UniTask.FromResult(LocalPlayerApi.ToSnapshot(save));
        }
    }
}
