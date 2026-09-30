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
            var (current, _) = EnergyRule.At(save.wallet.energyStored, EnergyRule.ParseUpdatedAt(save.wallet.energyUpdatedAt), DateTime.UtcNow, _data.Energy);

            current += source switch
            {
                EnergySource.Ad => _data.Energy.AdRecoverAmount,
                EnergySource.Purchase => _data.Energy.PurchaseRecoverAmount,
                _ => 0,
            };

            save.wallet.energyStored = current;
            save.wallet.energyUpdatedAt = DateTime.UtcNow.ToString("O");
            _store.Flush(save);
            return UniTask.FromResult(LocalPlayerApi.ToSnapshot(save));
        }
    }
}
