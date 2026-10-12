using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Game.Core
{
    /// <summary>랜덤 재료 보상에서 나올 수 있는 재료 하나와 그 확률(0~1)</summary>
    public readonly struct RewardOdd
    {
        public string ItemId { get; }
        public float Probability { get; }

        public RewardOdd(string itemId, float probability)
        {
            ItemId = itemId;
            Probability = probability;
        }
    }

    /// <summary>확률형 보상(랜덤 마법북·랜덤 인챈트 주문서)의 후보별 획득 확률. 후보 목록은 지급과 같은 RandomMaterialResolver.Candidates를 쓰고,
    /// 지급이 후보를 균등하게 뽑으므로 확률은 1/후보 수다. 열린 스킬·장비가 바뀌면 후보도 확률도 따라 바뀐다</summary>
    public static class RandomRewardOdds
    {
        public static bool IsRandom(string itemId) =>
            itemId == ItemIds.RandomSkillMaterial || itemId == ItemIds.RandomEquipmentMaterial;

        public static List<RewardOdd> For(string itemId, IEnumerable<int> unlockedSkills, IEnumerable<string> unlockedEquipment, GameDataStore data)
        {
            var candidates = RandomMaterialResolver.Candidates(itemId, unlockedSkills, unlockedEquipment, data);
            float each = candidates.Count == 0 ? 0f : 1f / candidates.Count;
            return candidates.Select(id => new RewardOdd(id, each)).ToList();
        }

        /// <summary>경험치(exp)에 해당하는 플레이어 레벨에서 열린 스킬·장비 기준의 확률</summary>
        public static List<RewardOdd> ForExp(string itemId, GameDataStore data, int exp) =>
            For(itemId, SkillUnlockRule.UnlockedSkillIds(data, exp), EquipmentUnlockRule.UnlockedTargetIds(data, exp), data);

        /// <summary>표시용 문자열. 소수 둘째 자리까지 (예: 33.33%)</summary>
        public static string Percent(float probability) =>
            (probability * 100f).ToString("0.00", CultureInfo.InvariantCulture) + "%";
    }
}
