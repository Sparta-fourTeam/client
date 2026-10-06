using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    public static class SkillMaterialResolver
    {
        /// <summary>랜덤 재료만 실제 재료로 변환한다. 분배 정책은 함수로 교체할 수 있다.</summary>
        public static List<ItemAmount> Resolve(IEnumerable<ItemAmount> rewards, IEnumerable<int> unlockedSkills,
            GameDataStore data, int seed, Func<int, int, int[]> distribute = null)
        {
            var result = new List<ItemAmount>();
            var random = new Random(seed);
            foreach (var item in rewards)
            {
                if (item.itemId != ItemIds.RandomSkillMaterial)
                { result.Add(new ItemAmount { itemId = item.itemId, quantity = item.quantity }); continue; }
                if (item.quantity <= 0) { throw new ApiException(ApiErrorKind.Rejected, "INVALID_ITEM_REWARD"); }
                var unlocked = new HashSet<int>(unlockedSkills ?? Array.Empty<int>());
                var candidates = ItemIds.SkillMaterials.Where(id =>
                    int.TryParse(data.Items.GetOrThrow(id).TargetId, out var skillId) && unlocked.Contains(skillId)).ToList();
                if (candidates.Count == 0) { throw new ApiException(ApiErrorKind.Rejected, "NO_UNLOCKED_SKILL_MATERIAL"); }
                // 비복원 균등 선택. 실제 지급 종류는 수량과 후보 수에 따라 최대 2종이다.
                int count = Math.Min(2, Math.Min(candidates.Count, item.quantity));
                for (int i = 0; i < count; i++)
                {
                    int selected = random.Next(i, candidates.Count);
                    (candidates[i], candidates[selected]) = (candidates[selected], candidates[i]);
                }
                var amounts = (distribute ?? SkillMaterialDistribution.EqualSplit)(item.quantity, count);
                if (amounts == null || amounts.Length != count || amounts.Any(amount => amount < 0)
                    || amounts.Sum(amount => (long)amount) != item.quantity)
                { throw new ApiException(ApiErrorKind.Rejected, "INVALID_MATERIAL_DISTRIBUTION"); }
                for (int i = 0; i < count; i++)
                {
                    if (amounts[i] > 0) { result.Add(new ItemAmount { itemId = candidates[i], quantity = amounts[i] }); }
                }
            }
            return result;
        }
    }

    public static class SkillMaterialDistribution
    {
        /// <summary>분배 방식 확정 전의 임시 정책: 균등 분배하고 나머지는 앞 후보부터 준다.</summary>
        public static int[] EqualSplit(int quantity, int count) => Enumerable.Range(0, count)
            .Select(index => quantity / count + (index < quantity % count ? 1 : 0)).ToArray();
    }
}
