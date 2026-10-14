using Cysharp.Threading.Tasks;

namespace Game.Core
{
    /// <summary>일일·주간 퀘스트 보상 수령을 담당하는 API. 진행(로그인·에너지 소모·처치)은 서버가 로그인·전투 API에서 직접 센다</summary>
    public interface IQuestApi
    {
        // TODO(server): idempotencyKey 없음 — 예: ClaimQuest(string questId, string idempotencyKey)
        /// <summary>목표를 채운 퀘스트를 받아 보드 점수를 올리고 반영된 스냅샷을 받는다.
        /// 거절 코드: NOT_COMPLETE(목표 미달), ALREADY_CLAIMED, UNKNOWN_DATA_ID</summary>
        UniTask<PlayerSnapshot> ClaimQuest(string questId);

        /// <summary>게이지가 닿은 보물상자를 열어 골드·에너지를 받고 반영된 스냅샷을 받는다.
        /// 거절 코드: NOT_REACHED(점수 미달), ALREADY_CLAIMED, UNKNOWN_DATA_ID</summary>
        UniTask<PlayerSnapshot> ClaimQuestChest(string chestId);
    }
}
