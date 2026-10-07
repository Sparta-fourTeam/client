using Cysharp.Threading.Tasks;
using Game.Core;

namespace Game.Network
{
    /// <summary>LocalSave를 PlayerSnapshot으로 변환해 돌려주는 Mock 구현</summary>
    public sealed class LocalPlayerApi : IPlayerApi
    {
        private readonly LocalSaveStore _store;

        public LocalPlayerApi(LocalSaveStore store)
        {
            _store = store;
        }

        public UniTask<PlayerSnapshot> GetMe() => UniTask.FromResult(ToSnapshot(_store.Load()));

        /// <summary>다른 Local*Api도 응답을 만들 때 이 변환을 재사용한다</summary>
        internal static PlayerSnapshot ToSnapshot(LocalSave save) => new()
        {
            gold = save.wallet.gold,
            exp = save.exp,
            energyStored = save.wallet.energyStored,
            energyUpdatedAt = save.wallet.energyUpdatedAt,
            stageProgress = save.stageProgress,
            upgrades = save.upgrades,
            equipments = save.equipments ?? new(),
            items = save.items ?? new()
        };
    }
}
