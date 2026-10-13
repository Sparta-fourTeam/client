using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>자식 스킬이 시전될 때 효과를 가져올 다른 스킬의 카드 하나. 그 카드를 얻은 횟수만큼 효과가 적용된다.</summary>
    public class MirroredCard
    {
        public int skillId;
        public string cardId;
    }

    /// <summary>자식 스킬의 mirrorCards를 지금 상태로 풀어 적용할 효과 목록으로 만든다.
    /// 예: 집중 화살은 화살의 일제 사격 카드를 얻은 횟수만큼 발사 수와 피해 효과를 받는다. 영구 레벨 변형((+))도 그대로 따른다.</summary>
    public static class MirroredEffects
    {
        public static List<EffectDef> For(SkillData child, IReadOnlyDictionary<int, SkillData> catalog, IUpgradeState state)
        {
            var effects = new List<EffectDef>();
            if (child?.mirrorCards == null || state == null) { return effects; }
            foreach (var mirrored in child.mirrorCards)
            {
                if (mirrored == null || !catalog.TryGetValue(mirrored.skillId, out var owner)) { continue; }
                int count = state.GetAcquiredCount(mirrored.skillId, mirrored.cardId);
                var option = owner.upgrades?.Find(c => c.id == mirrored.cardId);
                if (count <= 0 || option == null
                    || !SkillUpgradeResolver.TryResolve(option, state.GetPermanentWeaponLevel(mirrored.skillId), out var resolved)) { continue; }
                for (int i = 0; i < count; i++) { effects.AddRange(resolved.Effects); }
            }
            return effects;
        }
    }
}
