using Cysharp.Threading.Tasks;

namespace Game.Core
{
    /// <summary>스테이지 클리어 보상 수령을 담당하는 API</summary>
    public interface IStageApi
    {
        // TODO(server): idempotencyKey 없음 — 예: ClaimRatingReward(int stageId, string idempotencyKey)
        /// <summary>안 받은 rating 보상을 수령하고 반영된 스냅샷을 받는다.</summary>
        UniTask<PlayerSnapshot> ClaimRatingReward(int stageId);
    }
}
