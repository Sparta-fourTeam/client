using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    public static class RandomMaterialResolver
    {
        public const int MaxPicks = 2;

        /// <summary>랜덤 스킬·장비 재료만 열린 재료로 변환한다. 분배 정책은 함수로 교체할 수 있다.</summary>
        public static List<ItemAmount> Resolve(IEnumerable<ItemAmount> rewards, IEnumerable<int> unlockedSkills,
            IEnumerable<string> unlockedEquipment, GameDataStore data, int seed, Func<int, int, int[]> distribute = null)
        {
            var result = new List<ItemAmount>();
            var random = new Random(seed);
            foreach (var item in rewards)
            {
                bool isRandom = data.RewardRules.Items.TryGetValue(item.itemId, out var rule) && rule.Random;
                bool isSkill = isRandom && rule.Kind == "SkillMaterial";
                if (!isRandom)
                { result.Add(new ItemAmount { itemId = item.itemId, quantity = item.quantity }); continue; }
                if (item.quantity <= 0) { throw new ApiException(ApiErrorKind.Rejected, "INVALID_ITEM_REWARD"); }

                var candidates = Candidates(item.itemId, unlockedSkills, unlockedEquipment, data);
                if (candidates.Count == 0)
                { throw new ApiException(ApiErrorKind.Rejected, isSkill ? "NO_UNLOCKED_SKILL_MATERIAL" : "NO_UNLOCKED_EQUIPMENT_MATERIAL"); }

                // 균등 복원 추출이라 같은 재료가 두 번 뽑힐 수 있다. 수량이 1이면 1종만 뽑는다.
                int picks = Math.Min(MaxPicks, item.quantity);
                var amounts = (distribute ?? RandomMaterialDistribution.EqualSplit)(item.quantity, picks);
                if (amounts == null || amounts.Length != picks || amounts.Any(amount => amount < 0)
                    || amounts.Sum(amount => (long)amount) != item.quantity)
                { throw new ApiException(ApiErrorKind.Rejected, "INVALID_MATERIAL_DISTRIBUTION"); }

                var picked = new Dictionary<string, int>();
                for (int i = 0; i < picks; i++)
                {
                    if (amounts[i] == 0) { continue; }
                    var id = candidates[random.Next(candidates.Count)];
                    picked[id] = checked((picked.TryGetValue(id, out var current) ? current : 0) + amounts[i]);
                }
                result.AddRange(picked.Select(entry => new ItemAmount { itemId = entry.Key, quantity = entry.Value }));
            }
            return result;
        }

        /// <summary>랜덤 재료가 지급될 수 있는 재료 ID들. 지급(Resolve)과 확률 안내(RandomRewardOdds)가 같은 목록을 쓴다.
        /// 랜덤 재료가 아니면 빈 목록이다</summary>
        public static List<string> Candidates(string randomItemId, IEnumerable<int> unlockedSkills,
            IEnumerable<string> unlockedEquipment, GameDataStore data)
        {
            if (!data.RewardRules.Items.TryGetValue(randomItemId, out var rule) || !rule.Random) { return new List<string>(); }
            if (rule.Kind == "SkillMaterial") { return SkillCandidates(unlockedSkills, data); }
            if (rule.Kind == "EquipmentMaterial") { return EquipmentCandidates(unlockedEquipment, data); }
            return new List<string>();
        }

        private static List<string> SkillCandidates(IEnumerable<int> unlocked, GameDataStore data)
        {
            var ids = new HashSet<int>(unlocked ?? Array.Empty<int>());
            return data.RewardRules.Items.Where(item => !item.Value.Random && item.Value.Kind == "SkillMaterial").Select(item => item.Key).Where(id =>
                int.TryParse(data.Items.GetOrThrow(id).TargetId, out var skillId) && ids.Contains(skillId)).ToList();
        }

        private static List<string> EquipmentCandidates(IEnumerable<string> unlocked, GameDataStore data)
        {
            var ids = new HashSet<string>(unlocked ?? Array.Empty<string>(), StringComparer.Ordinal);
            return data.RewardRules.Items.Where(item => !item.Value.Random && item.Value.Kind == "EquipmentMaterial")
                .Select(item => item.Key).Where(id => ids.Contains(data.Items.GetOrThrow(id).TargetId)).ToList();
        }
    }

    public static class RandomMaterialDistribution
    {
        /// <summary>균등 분배하고 나머지는 앞 선택부터 준다.</summary>
        public static int[] EqualSplit(int quantity, int count) => Enumerable.Range(0, count)
            .Select(index => quantity / count + (index < quantity % count ? 1 : 0)).ToArray();
    }
}
