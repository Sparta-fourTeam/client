using Cysharp.Threading.Tasks;

namespace Game.Core
{
    /// <summary>전투 발급과 결과 제출을 담당하는 API</summary>
    public interface IBattleApi
    {
        /// <summary>스테이지 전투를 발급받는다 (battleId, seed 포함)</summary>
        UniTask<StartBattleResponse> StartBattle(int stageId, int dataRevision);

        /// <summary>전투 결과를 제출한다. 재시도 시에도 처음 만든 요청을 그대로 재사용해야 한다</summary>
        UniTask<SubmitResultResponse> SubmitResult(SubmitResultRequest req);
    }
}
