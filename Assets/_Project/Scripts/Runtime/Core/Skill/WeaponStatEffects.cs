using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>Compiles ordered card effects into one snapshot without a decorator chain.</summary>
    public static class WeaponStatEffects
    {
        public static bool TryApply(WeaponStats current, IReadOnlyList<EffectDef> effects, out WeaponStats result)
        {
            result = null;
            if (current == null || effects == null) { return false; }
            var prepared = new SkillConfigBuilder(current);
            if (!prepared.TryApplyCatalog(effects)) { return false; }
            result = prepared.Build().Stats;
            return true;
        }

        public static bool TryCompile(EffectDef effect, out IUpgradeEffect compiled)
        {
            compiled = null;
            if (effect == null || float.IsNaN(effect.value) || float.IsInfinity(effect.value)
                || !EffectRegistry.TryGet(effect.kind, out var kind)) { return false; }
            if (kind.Compile != null)
            {
                compiled = kind.Compile(effect);
                return compiled != null;
            }
            if (!kind.Accept(effect.value)) { return false; }
            compiled = kind.Category switch
            {
                UpgradeEffectCategory.Cast => new CastUpgradeEffect(effect.kind, effect.value),
                UpgradeEffectCategory.Transform => new TransformUpgradeEffect((WeaponForm)(int)effect.value),
                UpgradeEffectCategory.Reaction => new ReactionStatUpgradeEffect(effect.kind, effect.value),
                _ => new StatUpgradeEffect(effect.kind, effect.value)
            };
            return true;
        }

        internal static bool TryApplyValue(WeaponStatsBuilder builder, string key, float value, UpgradeEffectCategory category)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)
                || !EffectRegistry.TryGet(key, out var kind) || category != kind.Category || !kind.Accept(value)) { return false; }
            kind.Apply(builder, value);
            return true;
        }
    }
}
