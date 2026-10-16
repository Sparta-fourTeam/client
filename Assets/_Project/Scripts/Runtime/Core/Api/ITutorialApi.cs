using Cysharp.Threading.Tasks;

namespace Game.Core
{
    /// <summary>튜토리얼 진행 저장을 담당하는 API</summary>
    public interface ITutorialApi
    {
        /// <summary>튜토리얼(Tutorials.json)을 완료로 저장하고 그 보상을 준다. 이미 완료한 튜토리얼은 보상 없이 현재 스냅샷을 준다.
        /// `<튜토리얼Id>.dialogue`는 그 튜토리얼의 도입 대사를 봤다는 기록으로, 조건 없이 저장하고 보상은 없다.
        /// 거절 코드: UNKNOWN_TUTORIAL, NOT_READY(시작·완료 조건 미충족)</summary>
        UniTask<TutorialCompleteResponse> Complete(string tutorialId);
    }
}
