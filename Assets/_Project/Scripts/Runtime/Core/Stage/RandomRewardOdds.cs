using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Game.Core
{
    /// <summary>확률형 보상의 후보 하나와 한 번 뽑을 때 나올 확률(0~1)</summary>
    public readonly struct RewardOdds
    {
        public string ItemId { get; }
        public double Probability { get; }

        public RewardOdds(string itemId, double probability)
        {
            ItemId = itemId;
            Probability = probability;
        }
    }

    /// <summary>확률형 보상의 공개용 확률. 후보는 지급 로직(RandomMaterialResolver)과 같은 목록을,
    /// 해금은 현재 플레이어 레벨 기준(SkillUnlockRule·EquipmentUnlockRule)을 쓰므로 테이블이 바뀌면 표시도 따라 바뀐다</summary>
    public static class RandomRewardOdds
    {
        public static bool IsRandom(string itemId) =>
            itemId == ItemIds.RandomSkillMaterial || itemId == ItemIds.RandomEquipmentMaterial;

        /// <summary>보상 목록 중 확률형 보상의 ID. 같은 ID는 한 번만, 목록 순서대로</summary>
        public static List<string> RandomItemsOf(IEnumerable<ItemAmount> items) =>
            (items ?? Array.Empty<ItemAmount>()).Where(item => item != null && IsRandom(item.itemId))
                .Select(item => item.itemId).Distinct().ToList();

        /// <summary>누적 경험치 exp인 플레이어가 randomItemId를 한 번 뽑을 때 후보별 확률. 균등 추출이라 모두 1/후보 수다.
        /// 후보가 없으면 빈 목록이다(지급 시 NO_UNLOCKED_* 로 거절되는 상태)</summary>
        public static List<RewardOdds> For(string randomItemId, GameDataStore data, int exp)
        {
            if (!IsRandom(randomItemId)) { return new List<RewardOdds>(); }
            var candidates = RandomMaterialResolver.Candidates(randomItemId,
                randomItemId == ItemIds.RandomSkillMaterial ? SkillUnlockRule.UnlockedSkillIds(data, exp) : null,
                randomItemId == ItemIds.RandomEquipmentMaterial ? EquipmentUnlockRule.UnlockedTargetIds(data, exp) : null,
                data);
            return Uniform(candidates);
        }

        /// <summary>후보 목록의 균등 확률. 중복 후보는 그만큼 확률이 커진다</summary>
        public static List<RewardOdds> Uniform(IReadOnlyList<string> candidates)
        {
            if (candidates == null || candidates.Count == 0) { return new List<RewardOdds>(); }
            return candidates.GroupBy(id => id, StringComparer.Ordinal)
                .Select(group => new RewardOdds(group.Key, (double)group.Count() / candidates.Count)).ToList();
        }

        /// <summary>확률(0~1)을 소수점 decimals자리 백분율 문자열로 바꾼다. 반올림하고, 0보다 큰 확률이 0으로 보이지 않게 최소 단위로 올린다</summary>
        public static string FormatPercent(double probability, int decimals)
        {
            if (decimals < 0 || decimals > 6) { throw new ArgumentOutOfRangeException(nameof(decimals)); }
            if (double.IsNaN(probability) || probability < 0 || probability > 1)
            { throw new ArgumentOutOfRangeException(nameof(probability)); }
            double percent = Math.Round(probability * 100, decimals, MidpointRounding.AwayFromZero);
            double step = Math.Pow(10, -decimals);
            if (probability > 0 && percent < step) { percent = step; }
            return percent.ToString("F" + decimals, CultureInfo.InvariantCulture) + "%";
        }
    }
}
