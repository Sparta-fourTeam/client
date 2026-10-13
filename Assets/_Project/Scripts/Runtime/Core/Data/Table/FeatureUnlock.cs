using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>스테이지를 클리어해야 열리는 로비 기능 한 줄 (ClientData/FeatureUnlocks.json).
    /// Feature는 화면 쪽 FeatureLockView의 기능 이름(character, inventory, shop, quest 등)과 맞춘다</summary>
    [Serializable]
    public sealed class FeatureUnlock
    {
        public string Feature;
        public int StageCleared;
    }

    /// <summary>기능 이름 → 열리는 데 필요한 클리어 스테이지. 표에 없는 기능은 처음부터 열려 있다</summary>
    public sealed class FeatureUnlockTable
    {
        private readonly Dictionary<string, int> _requiredStage = new();

        public FeatureUnlockTable(IEnumerable<FeatureUnlock> rows, Func<int, bool> stageExists)
        {
            foreach (var row in rows ?? Array.Empty<FeatureUnlock>())
            {
                if (row == null || string.IsNullOrWhiteSpace(row.Feature) || !_requiredStage.TryAdd(row.Feature, row.StageCleared))
                {
                    throw new InvalidOperationException($"FeatureUnlocks {row?.Feature}: Feature가 비었거나 중복됩니다");
                }

                if (!stageExists(row.StageCleared))
                {
                    throw new InvalidOperationException($"FeatureUnlocks {row.Feature}: 스테이지 {row.StageCleared}이(가) Stages에 없습니다");
                }
            }
        }

        /// <summary>열리는 데 필요한 클리어 스테이지. 처음부터 열린 기능이면 0</summary>
        public int RequiredStage(string feature) => feature != null && _requiredStage.TryGetValue(feature, out int stage) ? stage : 0;

        public bool IsLocked(string feature, Func<int, bool> isStageCleared)
        {
            int stage = RequiredStage(feature);
            return stage > 0 && !isStageCleared(stage);
        }
    }
}
