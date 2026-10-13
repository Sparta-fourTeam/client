using Cysharp.Threading.Tasks;

namespace Game.Core
{
    /// <summary>몬스터 도감 등록 보상 수령을 담당하는 API</summary>
    public interface ICodexApi
    {
        // TODO(server): idempotencyKey 없음 — 예: ClaimMonsterReward(int monsterId, string idempotencyKey)
        /// <summary>도감에 등록된 몬스터의 보상을 수령하고 반영된 스냅샷을 받는다.
        /// 거절 코드: NOT_REGISTERED(마주친 적 없음), ALREADY_CLAIMED(이미 수령함), UNKNOWN_DATA_ID(없는 몬스터)</summary>
        UniTask<PlayerSnapshot> ClaimMonsterReward(int monsterId);
    }
}
