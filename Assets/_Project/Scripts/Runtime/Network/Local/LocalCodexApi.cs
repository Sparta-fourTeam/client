using Cysharp.Threading.Tasks;
using Game.Core;

namespace Game.Network
{
    /// <summary>도감 등록 보상 수령을 로컬 저장소에 반영하는 Mock 구현</summary>
    public sealed class LocalCodexApi : ICodexApi
    {
        private readonly LocalSaveStore _store;
        private readonly GameDataStore _data;

        public LocalCodexApi(LocalSaveStore store, GameDataStore data)
        {
            _store = store;
            _data = data;
        }

        public UniTask<PlayerSnapshot> ClaimMonsterReward(int monsterId)
        {
            var monster = _data.Monsters.GetOrThrow(monsterId);
            var save = _store.Load();
            var row = save.codex?.Find(c => c.monsterId == monsterId);
            if (row == null)
            {
                throw new ApiException(ApiErrorKind.Rejected, "NOT_REGISTERED");
            }

            if (row.claimed)
            {
                throw new ApiException(ApiErrorKind.Rejected, "ALREADY_CLAIMED");
            }

            int gold;
            try { gold = checked(save.wallet.gold + _data.CodexRewardGold[monster.GetEnemyType()]); }
            catch (System.OverflowException) { throw new ApiException(ApiErrorKind.Rejected, "REWARD_QUANTITY_OVERFLOW"); }

            row.claimed = true;
            save.wallet.gold = gold;
            _store.Flush(save);

            return UniTask.FromResult(LocalPlayerApi.ToSnapshot(save));
        }
    }
}
