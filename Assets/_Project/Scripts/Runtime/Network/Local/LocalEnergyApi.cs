using System;
using Cysharp.Threading.Tasks;
using Game.Core;

namespace Game.Network
{
    /// <summary>에너지 회복을 로컬 저장소에 반영하는 Mock 구현</summary>
    public sealed class LocalEnergyApi : IEnergyApi
    {
        private readonly LocalSaveStore _store;
        private readonly GameDataStore _data;

        public LocalEnergyApi(LocalSaveStore store, GameDataStore data)
        {
            _store = store;
            _data = data;
        }

        public UniTask<PlayerSnapshot> Recover(EnergySource source, string idempotencyKey = null)
        {
            var save = _store.Load();
            if (source == EnergySource.Ad)
            {
                if (string.IsNullOrWhiteSpace(idempotencyKey))
                { throw new ApiException(ApiErrorKind.Rejected, "INVALID_IDEMPOTENCY_KEY"); }
                save.ledger ??= new();
                if (save.ledger.Exists(row => row.sourceType == "ENERGY_AD" && row.idempotencyKey == idempotencyKey))
                { return UniTask.FromResult(LocalPlayerApi.ToSnapshot(save)); }
            }
            var now = DateTime.UtcNow;
            var updatedAt = EnergyRule.ParseUpdatedAt(save.wallet.energyUpdatedAt);
            var (current, _) = EnergyRule.At(save.wallet.energyStored, updatedAt, now, _data.Energy);
            var settledAt = EnergyRule.SettledUpdatedAt(current, updatedAt, now, _data.Energy);

            current += source switch
            {
                EnergySource.Ad => _data.Energy.AdRecoverAmount,
                EnergySource.Purchase => _data.Energy.PurchaseRecoverAmount,
                _ => 0,
            };

            save.wallet.energyStored = current;
            save.wallet.energyUpdatedAt = (current >= _data.Energy.Max ? now : settledAt).ToString("O");
            if (source == EnergySource.Ad)
            {
                save.ledger.Add(new LedgerRow
                { idempotencyKey = idempotencyKey, amount = _data.Energy.AdRecoverAmount, sourceType = "ENERGY_AD" });
            }
            _store.Flush(save);
            return UniTask.FromResult(LocalPlayerApi.ToSnapshot(save));
        }
    }
}
