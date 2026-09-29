using Cysharp.Threading.Tasks;
using Game.Core;

namespace Game.Network
{
    /// <summary>스테이지 rating 보상 수령을 로컬 저장소에 반영하는 Mock 구현</summary>
    public sealed class LocalStageApi : IStageApi
    {
        private readonly LocalSaveStore _store;
        private readonly GameDataStore _data;

        public LocalStageApi(LocalSaveStore store, GameDataStore data)
        {
            _store = store;
            _data = data;
        }

        public UniTask<PlayerSnapshot> ClaimRatingReward(int stageId)
        {
            var save = _store.Load();
            var progress = save.stageProgress.Find(p => p.stageId == stageId);
            if (progress == null)
            {
                throw new ApiException(ApiErrorKind.Rejected, "INVALID_ID");
            }

            if (!progress.HasUnclaimedReward)
            {
                throw new ApiException(ApiErrorKind.Rejected, "NO_REWARD");
            }

            var stage = _data.Stages.GetOrThrow(stageId);
            int total = 0;
            for (int rating = progress.claimedRating + 1; rating <= progress.clearRating; rating++)
            {
                total += stage.RatingRewards[rating - 1];
            }

            progress.claimedRating = progress.clearRating;
            save.wallet.gold += total;
            _store.Flush(save);

            return UniTask.FromResult(LocalPlayerApi.ToSnapshot(save));
        }
    }
}
