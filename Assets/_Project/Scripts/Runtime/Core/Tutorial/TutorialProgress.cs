using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    /// <summary>튜토리얼 진행 판정 (Tutorials.json). 완료한 튜토리얼은 서버(ITutorialApi)에 저장되고 PlayerSnapshot.tutorials로 돌아온다.
    /// 아직 완료하지 않았고 앞 튜토리얼(After)과 레벨(MinLevel)을 채운 튜토리얼이 "시작된" 것이다.
    /// 시작됐지만 완료 조건을 못 채웠으면 진행 중이고, 채웠으면 완료(보상 받기)할 수 있다. 같은 종류끼리는 표에서 앞의 것이 먼저다</summary>
    public static class TutorialProgress
    {
        public static bool IsStarted(TutorialDefinition tutorial, TutorialFacts facts) =>
            !facts.IsDone(tutorial.Id)
            && (string.IsNullOrEmpty(tutorial.After) || facts.IsDone(tutorial.After))
            && facts.Level >= tutorial.MinLevel;

        public static bool IsInProgress(TutorialDefinition tutorial, TutorialFacts facts) =>
            IsStarted(tutorial, facts) && !tutorial.Complete.IsMet(facts);

        public static bool IsCompletable(TutorialDefinition tutorial, TutorialFacts facts) =>
            IsStarted(tutorial, facts) && tutorial.Complete.IsMet(facts);

        /// <summary>진행 중인 전투 튜토리얼의 스테이지. 없으면 0. 이 스테이지로는 로비 대신 바로 보내고 에너지를 받지 않는다</summary>
        public static int BattleStage(IEnumerable<TutorialDefinition> tutorials, TutorialFacts facts) =>
            tutorials.FirstOrDefault(t => t.IsBattle && IsInProgress(t, facts))?.StageId ?? 0;

        /// <summary>지금 이끌 가이드 튜토리얼. 없으면 null</summary>
        public static TutorialDefinition ActiveGuide(IEnumerable<TutorialDefinition> tutorials, TutorialFacts facts) =>
            tutorials.FirstOrDefault(t => t.IsGuide && IsInProgress(t, facts));

        public static List<TutorialDefinition> Completable(IEnumerable<TutorialDefinition> tutorials, TutorialFacts facts) =>
            tutorials.Where(t => IsCompletable(t, facts)).ToList();
    }
}
