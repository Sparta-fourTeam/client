using Cysharp.Threading.Tasks;

namespace Game.Core
{
    /// <summary>전투 누적 진행의 중간 접수. 결과 보상 정산과 별개이며 같은 요청으로 재시도한다.</summary>
    public interface IBattleProgressApi
    {
        string SourceRevision { get; }
        UniTask<BattleProgressAck> ReportProgress(BattleProgressRequest request);
        /// <summary>이어하지 않는 포기. 마지막 누적 접수와 세션 닫기이며 결과 보상은 없다.</summary>
        UniTask<BattleProgressAck> AbandonProgress(BattleProgressRequest request);
    }

    /// <summary>로컬 검증에 저장 당시 전투 원문이 필요한 구현의 체크포인트 선택 경로.</summary>
    public interface IBattleCheckpointProgressApi
    {
        IBattleProgressApi ForCheckpoint(BattleCheckpoint checkpoint);
    }
}
