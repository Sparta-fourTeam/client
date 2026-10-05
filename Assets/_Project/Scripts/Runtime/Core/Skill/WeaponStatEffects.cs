using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>Compiles ordered card effects into one snapshot without a decorator chain.</summary>
    public static class WeaponStatEffects
    {
        private sealed class Rule
        {
            public readonly UpgradeEffectCategory Category;
            public readonly Action<WeaponStatsBuilder, float> Apply;
            public readonly Func<float, bool> Accept;

            public Rule(UpgradeEffectCategory category, Action<WeaponStatsBuilder, float> apply, Func<float, bool> accept)
            {
                Category = category;
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
            compiled = rule.Category switch
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
            if (float.IsNaN(value) || float.IsInfinity(value)
                || !Rules.TryGetValue(type, out var rule) || category != rule.Category || !rule.Accept(value)) { return false; }
            rule.Apply(builder, value);
            return true;
        }

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

        // 분류(Category)는 규칙 선언에 함께 적는다. 따로 두는 switch가 없으므로 빠뜨려서 Stat으로 잘못 분류될 일이 없다.
        private static void Add(Dictionary<UpgradeType, Rule> rules, UpgradeType type, UpgradeEffectCategory category,
            Action<WeaponStatsBuilder, float> apply, Func<float, bool> accept = null) =>
            rules.Add(type, new Rule(category, apply, accept ?? (_ => true)));

        private static void AddStat(Dictionary<UpgradeType, Rule> rules, UpgradeType type,
            Action<WeaponStatsBuilder, float> apply, Func<float, bool> accept = null) =>
            Add(rules, type, UpgradeEffectCategory.Stat, apply, accept);

        private static void AddCast(Dictionary<UpgradeType, Rule> rules, UpgradeType type,
            Action<WeaponStatsBuilder, float> apply, Func<float, bool> accept = null) =>
            Add(rules, type, UpgradeEffectCategory.Cast, apply, accept);

        private static void AddReaction(Dictionary<UpgradeType, Rule> rules, UpgradeType type,
            Action<WeaponStatsBuilder, float> apply, Func<float, bool> accept = null) =>
            Add(rules, type, UpgradeEffectCategory.Reaction, apply, accept);

        private static bool Positive(float value) => value > 0;
        private static bool PositiveInteger(float value) => value > 0 && value == Math.Round(value);
        private static float Factor(float percent) => 1 + percent * .01f;

        private static void AddCastRules(Dictionary<UpgradeType, Rule> rules)
        {
            AddStat(rules, UpgradeType.AttackSpeed, (s, v) => s[Stat.Cooldown] = Math.Max(.1f, s[Stat.Cooldown] * (1 - v * .01f)));
            AddStat(rules, UpgradeType.Damage, (s, v) => { s[Stat.Damage] *= Factor(v); s[Stat.ExplosionDamage] *= Factor(v); });
            AddStat(rules, UpgradeType.ImpactDamage, (s, v) => s[Stat.Damage] *= Factor(v));
            AddCast(rules, UpgradeType.ProjectileCount, (s, v) => s[Stat.ProjectileCount] = Math.Max(1, s[Stat.ProjectileCount] + (int)Math.Round(v)));
            AddCast(rules, UpgradeType.HitCount, (s, v) => s[Stat.ProjectileCount] = Math.Max(1, s[Stat.ProjectileCount] + (int)v));
            AddCast(rules, UpgradeType.CastCount, (s, v) => s[Stat.CastCount] = Math.Max(1, s[Stat.CastCount] + (int)v));
            AddCast(rules, UpgradeType.ReserveCasts, (s, v) => s[Stat.ReserveCastCount] = (int)v, PositiveInteger);
            Add(rules, UpgradeType.Form, UpgradeEffectCategory.Transform, (s, v) => s[Stat.Form] = (int)v,
                v => v == (int)WeaponForm.Enbakutsu || v == (int)WeaponForm.JudgementThunder
                    || v == (int)WeaponForm.TriangleIce || v == (int)WeaponForm.LargeLog || v == (int)WeaponForm.FireLog);
        }

        private static void AddProjectileRules(Dictionary<UpgradeType, Rule> rules)
        {
            AddStat(rules, UpgradeType.PierceCount, (s, v) => s[Stat.PierceCount] = Math.Max(0, s[Stat.PierceCount] + (int)v));
            AddStat(rules, UpgradeType.ProjectileSpeed, (s, v) => s[Stat.ProjectileSpeed] = Math.Max(0, s[Stat.ProjectileSpeed] * Factor(v)));
            AddStat(rules, UpgradeType.ProjectileSize, (s, v) => s[Stat.ProjectileSizeMultiplier] *= Factor(v), Positive);
            AddStat(rules, UpgradeType.Knockback, (s, v) => s[Stat.KnockbackDistance] = Math.Max(0, s[Stat.KnockbackDistance] * Factor(v)));
            AddReaction(rules, UpgradeType.EnableExplosion, (s, v) => s[Stat.ExplosionRadius] = v, Positive);
            AddStat(rules, UpgradeType.ExplosionDamage, (s, v) => s[Stat.ExplosionDamage] *= Factor(v));
            AddStat(rules, UpgradeType.ExplosionRadius, (s, v) => s[Stat.ExplosionRadius] *= Factor(v));
        }

        private static void AddStatusRules(Dictionary<UpgradeType, Rule> rules)
        {
            AddReaction(rules, UpgradeType.FreezeDuration, (s, v) => s[Stat.FreezeDuration] = Math.Max(s[Stat.FreezeDuration], v), Positive);
            AddReaction(rules, UpgradeType.Frostbite, (s, v) => s[Stat.FrostbiteRatio] = v * .01f, Positive);
            AddReaction(rules, UpgradeType.Paralysis, (s, v) => s[Stat.ParalysisDuration] = Math.Max(s[Stat.ParalysisDuration], v), Positive);
            AddStat(rules, UpgradeType.ParalysisDuration, (s, v) => s[Stat.ParalysisDuration] += v, Positive);
            AddReaction(rules, UpgradeType.StunDuration, (s, v) => s[Stat.StunDuration] = Math.Max(s[Stat.StunDuration], v), Positive);
            AddReaction(rules, UpgradeType.SlowDuration, (s, v) => s[Stat.SlowDuration] += v, Positive);
            AddReaction(rules, UpgradeType.VulnerabilityRatio, (s, v) => s[Stat.VulnerabilityRatio] = Math.Max(s[Stat.VulnerabilityRatio], v * .01f), Positive);
            AddReaction(rules, UpgradeType.VulnerabilityDuration, (s, v) => s[Stat.VulnerabilityDuration] = Math.Max(s[Stat.VulnerabilityDuration], v), Positive);
        }

        private static void AddBurnRules(Dictionary<UpgradeType, Rule> rules)
        {
            AddReaction(rules, UpgradeType.BurnDuration, (s, v) => s[Stat.BurnDuration] = v, Positive);
            AddReaction(rules, UpgradeType.BurnRatio, (s, v) => s[Stat.BurnRatio] = v * .01f, Positive);
            AddReaction(rules, UpgradeType.BurnMaxHp, (s, v) => s[Stat.BurnMaxHpRatio] = v * .01f, Positive);
            AddReaction(rules, UpgradeType.BurnDeathExplosion, (s, v) => s[Stat.BurnDeathExplosion] = 1, v => v == 1);
        }

        private static void AddSecondaryRules(Dictionary<UpgradeType, Rule> rules)
        {
            AddReaction(rules, UpgradeType.SplitCount, (s, v) => s[Stat.SplitCount] += (int)v, PositiveInteger);
            AddStat(rules, UpgradeType.SplitDamage, (s, v) => s[Stat.SplitDamageMultiplier] *= Factor(v));
            AddReaction(rules, UpgradeType.SplitFrostbite, (s, v) => s[Stat.SplitFrostbiteRatio] = v * .01f, Positive);
            AddReaction(rules, UpgradeType.LightningStrike, (s, v) => s[Stat.LightningStrikeRatio] = v * .01f, Positive);
            AddReaction(rules, UpgradeType.SplitLightning, (s, v) => s[Stat.SplitLightningRatio] = v * .01f, Positive);
            AddReaction(rules, UpgradeType.SplitParalysis, (s, v) => s[Stat.SplitParalysisDuration] = Math.Max(s[Stat.SplitParalysisDuration], v), Positive);
            AddReaction(rules, UpgradeType.SplitExplosion, (s, v) => s[Stat.SplitExplosions] = 1, v => v == 1);
            AddReaction(rules, UpgradeType.KillLightning, (s, v) => s[Stat.KillLightningRatio] = v * .01f, Positive);
        }

        private static void AddFieldRules(Dictionary<UpgradeType, Rule> rules)
        {
            AddStat(rules, UpgradeType.FieldDuration, (s, v) => s[Stat.FieldDuration] += v, Positive);
            AddStat(rules, UpgradeType.FieldDamageFlat, (s, v) => s[Stat.FieldFlatDamage] += v, Positive);
            AddStat(rules, UpgradeType.FieldDamageMultiplier, (s, v) => s[Stat.FieldDamageMultiplier] *= Factor(v), Positive);
        }
    }
}
