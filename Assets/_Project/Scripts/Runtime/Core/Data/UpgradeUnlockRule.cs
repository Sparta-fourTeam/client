using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>강화의 해금 플레이어 레벨. 스킬이 열리는 레벨은 SkillData.unlockLevel 하나로만 정한다.
    /// SkillData.progressionId가 이 UpgradeId인 강화 행(스킬 영구 강화)은 그 스킬이 열리는 레벨을 따르고, 장비 같은 나머지는 행의 UnlockLevel을 쓴다</summary>
    public static class UpgradeUnlockRule
    {
        /// <summary>progressionId(= 스킬 강화 UpgradeId) → 스킬이 열리는 레벨</summary>
        public static Dictionary<string, int> SkillUnlockLevels(IEnumerable<SkillData> skills)
        {
            var result = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var skill in skills)
            {
                if (string.IsNullOrEmpty(skill.progressionId)) { continue; }
                // 같은 progressionId를 여러 스킬이 쓰면 가장 먼저 열리는 레벨을 따른다
                result[skill.progressionId] = result.TryGetValue(skill.progressionId, out var current)
                    ? Math.Min(current, skill.unlockLevel) : skill.unlockLevel;
            }

            return result;
        }

        public static int LevelOf(UpgradeDefinition def, IReadOnlyDictionary<string, int> skillUnlockLevels) =>
            skillUnlockLevels.TryGetValue(def.UpgradeId, out var level) ? level : def.UnlockLevel;

        /// <summary>스킬 강화 행이 자기 UnlockLevel을 따로 정하면 출처가 둘이 되므로 거절한다 (기본값 1만 허용)</summary>
        public static void Validate(UpgradeDefinition def, IReadOnlyDictionary<string, int> skillUnlockLevels)
        {
            if (skillUnlockLevels.ContainsKey(def.UpgradeId) && def.UnlockLevel != 1)
            {
                throw new InvalidOperationException(
                    $"Upgrades {def.UpgradeId}: 스킬 강화의 해금 레벨은 Skills.json의 unlockLevel로 정합니다 (이 행의 UnlockLevel은 쓰지 마세요)");
            }
        }
    }
}
