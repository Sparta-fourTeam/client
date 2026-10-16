using System;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Messages;
using GameAnalyticsSDK;
using UnityEngine;

namespace Game.Boot
{
    public sealed class GameAnalyticsService : IGameplayAnalytics, IAnalyticsCollectionControl
    {
        private bool _ready;

        public void SetCollectionAllowed(bool allowed)
        {
            _ready = false;
#if !UNITY_EDITOR
            if (!allowed)
            {
                // 이미 시작한 SDK의 이벤트 제출·로컬 캐시를 중지한다. SDK 전체 해제를 뜻하지 않는다.
                if (GameAnalytics.Initialized) { TrySend(() => GameAnalytics.SetEnabledEventSubmission(false, false)); }
                return;
            }
            Initialize();
            if (GameAnalytics.Initialized) { TrySend(() => GameAnalytics.SetEnabledEventSubmission(true)); }
#endif
        }

        private void Initialize()
        {
            // 에디터 플레이·테스트·샌드박스는 운영 통계에 넣지 않는다.
#if !UNITY_EDITOR
            TrySend(() =>
            {
                var settings = Resources.Load<GameAnalyticsSDK.Setup.Settings>("GameAnalytics/Settings");
                int index = settings == null ? -1 : settings.Platforms.IndexOf(Application.platform);
                if (index < 0 || string.IsNullOrWhiteSpace(settings.GetGameKey(index))
                    || string.IsNullOrWhiteSpace(settings.GetSecretKey(index)))
                {
                    Debug.LogWarning("GameAnalytics: 현재 플랫폼의 Game Key와 Secret Key를 설정해야 분석을 전송합니다.");
                    return;
                }

                if (!GameAnalytics.Initialized)
                {
                    new GameObject("GameAnalytics").AddComponent<GameAnalytics>();
                    GameAnalytics.Initialize();
                }
                _ready = true;
            });
#endif
        }

        public void BattleStarted(int stageId)
        {
            if (!_ready || stageId <= 0) { return; }
            TrySend(() => GameAnalytics.NewProgressionEvent(GAProgressionStatus.Start, "stage", $"stage_{stageId}"));
        }

        public void BattleEnded(int stageId, bool cleared, bool forfeited, int reachedWave, int kills, float playTime,
            IReadOnlyList<SkillDamage> skillDamages)
        {
            if (!_ready || stageId <= 0) { return; }
            string outcome = cleared ? "clear" : forfeited ? "forfeit" : "fail";
            TrySend(() => GameAnalytics.NewProgressionEvent(
                cleared ? GAProgressionStatus.Complete : GAProgressionStatus.Fail, "stage", $"stage_{stageId}", kills,
                new Dictionary<string, object>
                {
                    ["outcome"] = outcome,
                    ["reached_wave"] = reachedWave,
                    ["play_time_seconds"] = playTime
                }));
            foreach (var skill in skillDamages)
            {
                if (skill.Damage <= 0) { continue; }
                TrySend(() => GameAnalytics.NewDesignEvent(
                    $"battle:skill_damage:stage_{stageId}:skill_{skill.SkillId}", skill.Damage,
                    new Dictionary<string, object> { ["outcome"] = outcome }));
            }
        }

        public void CardPicked(int stageId, string cardId, int pickIndex, string source)
        {
            if (!_ready || stageId <= 0 || string.IsNullOrWhiteSpace(cardId)) { return; }
            TrySend(() => GameAnalytics.NewDesignEvent($"card:picked:stage_{stageId}:{source}:{cardId}",
                new Dictionary<string, object> { ["pick_index"] = pickIndex }));
        }

        private static void TrySend(Action action)
        {
            try { action(); }
            catch (Exception exception)
            {
                // 분석 SDK 실패로 카드 적용이나 결과 제출을 중단하지 않는다.
                Debug.LogWarning($"GameAnalytics: {exception.GetType().Name}");
            }
        }
    }
}
