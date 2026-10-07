using Cysharp.Threading.Tasks;
using Game.Core;

namespace Game.Network
{
    /// <summary>업그레이드 구매를 로컬 저장소에 반영하는 Mock 구현. 장비 강화와 스킬 강화가 같이 쓴다. idempotencyKey로 연타를 막는다.
    /// 거절 순서: 해금 전 → 최대 레벨 → 코인 부족 → 재료 부족. 거절되면 아무것도 바뀌지 않는다</summary>
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
            if (_data.PlayerLevels.At(save.exp).level < _data.UnlockLevelOf(def))
            {
                throw new ApiException(ApiErrorKind.Rejected, "LOCKED");
            }

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

            // 재료 소모가 실패하면 예외로 끝나고, 아직 아무것도 바꾸지 않았으므로 저장하지 않는다
            if (def.UsesMaterial)
            {
                LocalItemSpend.Spend(save, def.MaterialItemId, def.MaterialAt(level + 1));
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
