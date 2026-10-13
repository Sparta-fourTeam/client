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

        /// <summary>누적 경험치 exp의 플레이어 레벨에서 열려 있는 스킬의 ID 목록</summary>
        public static List<int> UnlockedSkillIds(GameDataStore data, int exp) =>
            UnlockedSkillIds(data.LoadSkills(), data.PlayerLevels.At(exp).level);
    }
}
