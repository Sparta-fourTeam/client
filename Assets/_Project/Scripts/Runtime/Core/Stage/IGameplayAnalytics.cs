using System.Collections.Generic;
using Game.Core.Messages;

namespace Game.Core
{
    /// <summary>선택적 분석 수집의 시작·중지. 약관 동의와 별도로 결정한다.</summary>
    public interface IAnalyticsCollectionControl
    {
        void SetCollectionAllowed(bool allowed);
    }

    /// <summary>게임 진행 분석 경계. 구현은 SDK 오류가 게임 진행으로 전파되지 않게 한다.</summary>
    public interface IGameplayAnalytics
    {
        void BattleStarted(int stageId);
        void BattleEnded(int stageId, bool cleared, bool forfeited, int reachedWave, int kills, float playTime,
            IReadOnlyList<SkillDamage> skillDamages);
        void CardPicked(int stageId, string cardId, int pickIndex, string source);
    }
}
