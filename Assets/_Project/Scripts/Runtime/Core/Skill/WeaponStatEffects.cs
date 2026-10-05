using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>Compiles ordered card effects into one snapshot without a decorator chain.</summary>
    public static class WeaponStatEffects
    {
        private sealed class Rule
        {
            public readonly Action<WeaponStatsBuilder, float> Apply;
            public readonly Func<float, bool> Accept;

            public Rule(Action<WeaponStatsBuilder, float> apply, Func<float, bool> accept)
            {
                Apply = apply;
                Accept = accept;
            }
        }

        private static readonly Dictionary<UpgradeType, Rule> Rules = CreateRules();

        public static bool TryApply(WeaponStats current, IReadOnlyList<StatEffect> effects, out WeaponStats result)
        {
            result = null;
            if (current == null || effects == null) { return false; }
            var prepared = new SkillConfigBuilder(current);
            if (!prepared.TryApplyCatalog(effects)) { return false; }
            result = prepared.Build().Stats;
            return true;
        }

        public static bool TryCompile(StatEffect effect, out IUpgradeEffect compiled)
        {
            compiled = null;
            if (effect == null || float.IsNaN(effect.value) || float.IsInfinity(effect.value)
                || !Rules.TryGetValue(effect.type, out var rule) || !rule.Accept(effect.value)) { return false; }
            compiled = Category(effect.type) switch
            {
                UpgradeEffectCategory.Cast => new CastUpgradeEffect(effect.type, effect.value),
                UpgradeEffectCategory.Transform => new TransformUpgradeEffect((WeaponForm)(int)effect.value),
                UpgradeEffectCategory.Reaction => new NumericReactionUpgradeEffect(effect.type, effect.value),
                _ => new StatUpgradeEffect(effect.type, effect.value)
            };
            return true;
        }

        internal static bool TryApplyValue(WeaponStatsBuilder builder, UpgradeType type, float value, UpgradeEffectCategory category)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || category != Category(type)
                || !Rules.TryGetValue(type, out var rule) || !rule.Accept(value)) { return false; }
            rule.Apply(builder, value);
            return true;
        }

        private static UpgradeEffectCategory Category(UpgradeType type) => type switch
        {
            UpgradeType.Form => UpgradeEffectCategory.Transform,
            UpgradeType.ProjectileCount or UpgradeType.HitCount or UpgradeType.CastCount or UpgradeType.ReserveCasts => UpgradeEffectCategory.Cast,
            UpgradeType.EnableExplosion or UpgradeType.SplitCount or UpgradeType.FreezeDuration or UpgradeType.Frostbite
                or UpgradeType.ShardFrostbite or UpgradeType.Paralysis or UpgradeType.LightningStrike
                or UpgradeType.AuxiliaryLightning or UpgradeType.AuxiliaryExplosion or UpgradeType.BurnDuration
                or UpgradeType.BurnRatio or UpgradeType.BurnMaxHp or UpgradeType.BurnDeathExplosion
                or UpgradeType.AuxiliaryParalysis or UpgradeType.KillLightning or UpgradeType.StunDuration
                or UpgradeType.SlowDuration or UpgradeType.VulnerabilityRatio or UpgradeType.VulnerabilityDuration => UpgradeEffectCategory.Reaction,
            _ => UpgradeEffectCategory.Stat
        };

        private static Dictionary<UpgradeType, Rule> CreateRules()
        {
            var rules = new Dictionary<UpgradeType, Rule>();
            AddCastRules(rules);
            AddProjectileRules(rules);
            AddStatusRules(rules);
            AddBurnRules(rules);
            AddSecondaryRules(rules);
            AddFieldRules(rules);
            return rules;
        }

        private static void Add(Dictionary<UpgradeType, Rule> rules, UpgradeType type,
            Action<WeaponStatsBuilder, float> apply, Func<float, bool> accept = null) =>
            rules.Add(type, new Rule(apply, accept ?? (_ => true)));

        private static bool Positive(float value) => value > 0;
        private static bool PositiveInteger(float value) => value > 0 && value == Math.Round(value);
        private static float Factor(float percent) => 1 + percent * .01f;

        private static void AddCastRules(Dictionary<UpgradeType, Rule> rules)
        {
            Add(rules, UpgradeType.AttackSpeed, (s, v) => s.Cooldown = Math.Max(.1f, s.Cooldown * (1 - v * .01f)));
            Add(rules, UpgradeType.Damage, (s, v) => { s.Damage *= Factor(v); s.ExplosionDamage *= Factor(v); });
            Add(rules, UpgradeType.ImpactDamage, (s, v) => s.Damage *= Factor(v));
            Add(rules, UpgradeType.ProjectileCount, (s, v) => s.ProjectileCount = Math.Max(1, s.ProjectileCount + (int)Math.Round(v)));
            Add(rules, UpgradeType.HitCount, (s, v) => s.ProjectileCount = Math.Max(1, s.ProjectileCount + (int)v));
            Add(rules, UpgradeType.CastCount, (s, v) => s.CastCount = Math.Max(1, s.CastCount + (int)v));
            Add(rules, UpgradeType.ReserveCasts, (s, v) => s.ReserveCastCount = (int)v, PositiveInteger);
            Add(rules, UpgradeType.Form, (s, v) => s.Form = (WeaponForm)(int)v,
                v => v == (int)WeaponForm.Enbakutsu || v == (int)WeaponForm.JudgementThunder
                    || v == (int)WeaponForm.TriangleIce || v == (int)WeaponForm.LargeLog || v == (int)WeaponForm.FireLog);
        }

        private static void AddProjectileRules(Dictionary<UpgradeType, Rule> rules)
        {
            Add(rules, UpgradeType.PierceCount, (s, v) => s.PierceCount = Math.Max(0, s.PierceCount + (int)v));
            Add(rules, UpgradeType.ProjectileSpeed, (s, v) => s.ProjectileSpeed = Math.Max(0, s.ProjectileSpeed * Factor(v)));
            Add(rules, UpgradeType.ProjectileSize, (s, v) => s.ProjectileSizeMultiplier *= Factor(v), Positive);
            Add(rules, UpgradeType.Knockback, (s, v) => s.KnockbackDistance = Math.Max(0, s.KnockbackDistance * Factor(v)));
            Add(rules, UpgradeType.EnableExplosion, (s, v) => s.ExplosionRadius = v, Positive);
            Add(rules, UpgradeType.ExplosionDamage, (s, v) => s.ExplosionDamage *= Factor(v));
            Add(rules, UpgradeType.ExplosionRadius, (s, v) => s.ExplosionRadius *= Factor(v));
        }

        private static void AddStatusRules(Dictionary<UpgradeType, Rule> rules)
        {
            Add(rules, UpgradeType.FreezeDuration, (s, v) => s.FreezeDuration = Math.Max(s.FreezeDuration, v), Positive);
            Add(rules, UpgradeType.Frostbite, (s, v) => s.FrostbiteRatio = v * .01f, Positive);
            Add(rules, UpgradeType.Paralysis, (s, v) => s.ParalysisDuration = Math.Max(s.ParalysisDuration, v), Positive);
            Add(rules, UpgradeType.ParalysisDuration, (s, v) => s.ParalysisDuration += v, Positive);
            Add(rules, UpgradeType.StunDuration, (s, v) => s.StunDuration = Math.Max(s.StunDuration, v), Positive);
            Add(rules, UpgradeType.SlowDuration, (s, v) => s.SlowDuration += v, Positive);
            Add(rules, UpgradeType.VulnerabilityRatio, (s, v) => s.VulnerabilityRatio = Math.Max(s.VulnerabilityRatio, v * .01f), Positive);
            Add(rules, UpgradeType.VulnerabilityDuration, (s, v) => s.VulnerabilityDuration = Math.Max(s.VulnerabilityDuration, v), Positive);
        }

        private static void AddBurnRules(Dictionary<UpgradeType, Rule> rules)
        {
            Add(rules, UpgradeType.BurnDuration, (s, v) => s.BurnDuration = v, Positive);
            Add(rules, UpgradeType.BurnRatio, (s, v) => s.BurnRatio = v * .01f, Positive);
            Add(rules, UpgradeType.BurnMaxHp, (s, v) => s.BurnMaxHpRatio = v * .01f, Positive);
            Add(rules, UpgradeType.BurnDeathExplosion, (s, v) => s.BurnDeathExplosion = true, v => v == 1);
        }

        private static void AddSecondaryRules(Dictionary<UpgradeType, Rule> rules)
        {
            Add(rules, UpgradeType.SplitCount, (s, v) => s.SplitCount += (int)v, PositiveInteger);
            Add(rules, UpgradeType.ShardDamage, (s, v) => s.ShardDamageMultiplier *= Factor(v));
            Add(rules, UpgradeType.ShardFrostbite, (s, v) => s.ShardFrostbiteRatio = v * .01f, Positive);
            Add(rules, UpgradeType.LightningStrike, (s, v) => s.LightningStrikeRatio = v * .01f, Positive);
            Add(rules, UpgradeType.AuxiliaryLightning, (s, v) => s.AuxiliaryLightningRatio = v * .01f, Positive);
            Add(rules, UpgradeType.AuxiliaryParalysis, (s, v) => s.AuxiliaryParalysisDuration = Math.Max(s.AuxiliaryParalysisDuration, v), Positive);
            Add(rules, UpgradeType.AuxiliaryExplosion, (s, v) => s.AuxiliaryExplosions = true, v => v == 1);
            Add(rules, UpgradeType.KillLightning, (s, v) => s.KillLightningRatio = v * .01f, Positive);
        }

        private static void AddFieldRules(Dictionary<UpgradeType, Rule> rules)
        {
            Add(rules, UpgradeType.FieldDuration, (s, v) => s.FieldDuration += v, Positive);
            Add(rules, UpgradeType.FieldDamageFlat, (s, v) => s.FieldFlatDamage += v, Positive);
            Add(rules, UpgradeType.FieldDamageMultiplier, (s, v) => s.FieldDamageMultiplier *= Factor(v), Positive);
        }
    }
}
