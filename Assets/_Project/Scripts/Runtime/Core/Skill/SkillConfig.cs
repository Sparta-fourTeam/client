using System;
using System.Collections.Generic;

namespace Game.Core
{
    public readonly struct AttackDefinition
    {
        public CastType Type { get; }
        public ProjectilePath Path { get; }
        public float Range { get; }
        public AttackDefinition(CastType type, ProjectilePath path, float range)
        {
            Type = type;
            Path = path;
            Range = range;
        }
    }

    public sealed class SkillConfig
    {
        public WeaponStats Stats { get; }
        public AttackDefinition Attack { get; }
        public AttackReactions Reactions { get; }
        internal readonly ReactionBinding[] Bindings;

        internal SkillConfig(WeaponStats stats, AttackDefinition attack, IEnumerable<ReactionBinding> bindings)
        {
            Stats = stats;
            Attack = attack;
            Bindings = new List<ReactionBinding>(bindings).ToArray();
            Reactions = new AttackReactions(Bindings);
        }

        public static SkillConfig FromDefinition(WeaponData data) => new SkillConfig(
            WeaponStats.FromDefinition(data.baseStats), new AttackDefinition(data.castType, data.projectilePath, data.baseStats.range),
            Array.Empty<ReactionBinding>());
    }

    /// <summary>Builds an immutable configuration. Failed effect batches never change the builder.</summary>
    public sealed class SkillConfigBuilder
    {
        internal WeaponStatsBuilder Values { get; private set; }
        private readonly AttackDefinition attack;
        private List<ReactionBinding> bindings;

        public SkillConfigBuilder(SkillConfig config)
        {
            Values = new WeaponStatsBuilder(config.Stats);
            attack = config.Attack;
            bindings = new List<ReactionBinding>(config.Bindings);
        }

        internal SkillConfigBuilder(WeaponStats stats) : this(new SkillConfig(stats, default, Array.Empty<ReactionBinding>())) { }

        public void AddReaction(AttackEvent trigger, IAttackReaction reaction) => bindings.Add(new ReactionBinding(trigger, reaction));

        public bool TryApplyCatalog(IEnumerable<StatEffect> effects)
        {
            if (effects == null) { return false; }
            var compiled = new List<IUpgradeEffect>();
            foreach (var effect in effects)
            {
                if (!WeaponStatEffects.TryCompile(effect, out var upgrade)) { return false; }
                compiled.Add(upgrade);
            }
            return TryApply(compiled);
        }

        public bool TryApply(IEnumerable<IUpgradeEffect> effects)
        {
            if (effects == null) { return false; }
            var prepared = new SkillConfigBuilder(Build());
            foreach (var effect in effects)
            {
                if (effect == null || !effect.TryApply(prepared)) { return false; }
            }
            Values = prepared.Values;
            bindings = prepared.bindings;
            return true;
        }

        public SkillConfig Build() => new SkillConfig(new WeaponStats(Values), attack, bindings);
    }

    public interface IUpgradeEffect
    {
        bool TryApply(SkillConfigBuilder builder);
    }

    public enum UpgradeEffectCategory { Stat, Cast, Reaction, Transform }

    public abstract class NumericUpgradeEffect : IUpgradeEffect
    {
        private readonly UpgradeType type;
        private readonly float value;
        private readonly UpgradeEffectCategory category;
        protected NumericUpgradeEffect(UpgradeType type, float value, UpgradeEffectCategory category)
        {
            this.type = type;
            this.value = value;
            this.category = category;
        }
        public bool TryApply(SkillConfigBuilder builder) => WeaponStatEffects.TryApplyValue(builder.Values, type, value, category);
    }

    public sealed class StatUpgradeEffect : NumericUpgradeEffect
    {
        public StatUpgradeEffect(UpgradeType type, float value) : base(type, value, UpgradeEffectCategory.Stat) { }
    }

    public sealed class CastUpgradeEffect : NumericUpgradeEffect
    {
        public CastUpgradeEffect(UpgradeType type, float value) : base(type, value, UpgradeEffectCategory.Cast) { }
    }

    public sealed class TransformUpgradeEffect : NumericUpgradeEffect
    {
        public TransformUpgradeEffect(WeaponForm form) : base(UpgradeType.Form, (int)form, UpgradeEffectCategory.Transform) { }
    }

    public sealed class ReactionUpgradeEffect : IUpgradeEffect
    {
        private readonly ReactionBinding binding;
        public ReactionUpgradeEffect(AttackEvent trigger, IAttackReaction reaction) => binding = new ReactionBinding(trigger, reaction);
        public bool TryApply(SkillConfigBuilder builder)
        {
            builder.AddReaction(binding.Event, binding.Reaction);
            return true;
        }
    }

    // Adapts existing numeric JSON effects while new reactions can be authored directly.
    internal sealed class NumericReactionUpgradeEffect : NumericUpgradeEffect
    {
        public NumericReactionUpgradeEffect(UpgradeType type, float value) : base(type, value, UpgradeEffectCategory.Reaction) { }
    }
}
