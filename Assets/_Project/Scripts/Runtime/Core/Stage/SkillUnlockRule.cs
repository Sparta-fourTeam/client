using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    /// <summary>열린 스킬 판정. 스킬은 플레이어 레벨이 SkillData.unlockLevel 이상이면 열린다.
    /// 랜덤 스킬재료는 열린 스킬의 재료 중에서만 뽑는다 (장비는 EquipmentUnlockRule)</summary>
    public static class SkillUnlockRule
    {
        /// <summary>플레이어 레벨 level에서 열려 있는 스킬의 ID 목록</summary>
        public static List<int> UnlockedSkillIds(IEnumerable<SkillData> skills, int level) =>
            skills.Where(skill => skill.unlockLevel <= level).Select(skill => skill.id).ToList();

        /// <summary>플레이어 레벨이 fromLevel에서 toLevel로 오르며 새로 열린 무기. 전투 중 다른 스킬이 만드는 자식 스킬(childOnly)은 무기가 아니라 뺀다</summary>
        public static List<SkillData> NewlyUnlocked(IEnumerable<SkillData> skills, int fromLevel, int toLevel) =>
            skills.Where(skill => !skill.childOnly && skill.unlockLevel > fromLevel && skill.unlockLevel <= toLevel)
                .OrderBy(skill => skill.unlockLevel).ThenBy(skill => skill.id).ToList();

        /// <summary>누적 경험치 exp의 플레이어 레벨에서 열려 있는 스킬의 ID 목록</summary>
        public static List<int> UnlockedSkillIds(GameDataStore data, int exp) =>
            UnlockedSkillIds(data.LoadSkills(), data.PlayerLevels.At(exp).level);
    }
}
