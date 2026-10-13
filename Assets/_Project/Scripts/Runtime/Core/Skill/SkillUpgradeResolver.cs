using System.Collections.Generic;

namespace Game.Core
{
    public sealed class ResolvedWeaponUpgrade
    {
        public string Name { get; }
        public string Description { get; }
        public List<EffectDef> Effects { get; }

        public ResolvedWeaponUpgrade(string name, string description, List<EffectDef> effects)
        {
            Name = name;
            Description = description;
            Effects = effects;
        }
    }

    /// <summary>표시와 적용에 같은 변형을 사용하며 원본 카탈로그를 변경하지 않는다.</summary>
    public static class SkillUpgradeResolver
    {
        public static bool TryResolve(SkillUpgradeOption option, int permanentLevel, out ResolvedWeaponUpgrade resolved)
        {
            resolved = null;
            if (option == null || permanentLevel < 0 || !ValidEffects(option.effects)) { return false; }
            string name = option.name;
            string description = option.desc;
            var effects = option.effects;
            int selectedLevel = 0;
            var levels = new HashSet<int>();
            if (option.variants != null)
            {
                foreach (var variant in option.variants)
                {
                    if (variant == null || variant.minPermanentLevel <= 0
                        || !levels.Add(variant.minPermanentLevel) || string.IsNullOrEmpty(variant.name)
                        || !ValidEffects(variant.effects)) { return false; }
                    if (variant.minPermanentLevel <= permanentLevel && variant.minPermanentLevel > selectedLevel)
                    {
                        selectedLevel = variant.minPermanentLevel;
                        name = variant.name;
                        description = variant.desc;
                        effects = variant.effects;
                    }
                }
            }
            resolved = new ResolvedWeaponUpgrade(name, description, effects);
            return true;
        }

        private static bool ValidEffects(List<EffectDef> effects)
        {
            if (effects == null) { return false; }
            foreach (var effect in effects)
            {
                if (effect == null || !EffectRegistry.IsRegistered(effect.kind)
                    || float.IsNaN(effect.value) || float.IsInfinity(effect.value)) { return false; }
            }
            return true;
        }
    }
}
