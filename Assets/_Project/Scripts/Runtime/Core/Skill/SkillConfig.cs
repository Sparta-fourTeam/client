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
        public SkillStats Stats { get; }
        public AttackDefinition Attack { get; }
        public AttackReactions Reactions { get; }
        /// <summary>자식 스킬 시전 효과가 쓰는 시전기. 연결되지 않았으면 null</summary>
        public IChildSkillCaster ChildCaster { get; }
        /// <summary>시전하는 자식 스킬별 연결(부모 스탯 상속 규칙). 자식 ID로 찾는다</summary>
        public IReadOnlyDictionary<int, ChildLink> Children { get; }
        /// <summary>스킬 ID별로 보관한 효과. 이 부모 아래 어느 단계에서든 그 스킬이 시전될 때 그 스킬의 설정에 적용된다.
        /// 자식에게서 시전되는 손자에게도 같은 표가 전달된다.</summary>
        public IReadOnlyDictionary<int, IReadOnlyList<EffectDef>> Overlays { get; }
        internal readonly ReactionBinding[] Bindings;

        internal SkillConfig(SkillStats stats, AttackDefinition attack, IEnumerable<ReactionBinding> bindings,
            IChildSkillCaster childCaster = null, IReadOnlyDictionary<int, ChildLink> children = null,
            IReadOnlyDictionary<int, IReadOnlyList<EffectDef>> overlays = null)
        {
            Stats = stats;
            Attack = attack;
            ChildCaster = childCaster;
            Children = children ?? new Dictionary<int, ChildLink>();
            Overlays = overlays ?? new Dictionary<int, IReadOnlyList<EffectDef>>();
            Bindings = new List<ReactionBinding>(bindings).ToArray();
            var resolved = new List<ReactionBinding>(Bindings.Length);
            foreach (var binding in Bindings)
            {
                if (binding.TryResolve(this, out var bound)) { resolved.Add(bound); }
            }
            Reactions = new AttackReactions(resolved);
        }

        /// <summary>피해(직접·폭발)에 배율을 곱한 설정. 자식 스킬을 소형으로 시전할 때 쓴다</summary>
        public SkillConfig WithDamageScale(float scale)
        {
            var builder = new SkillStatsBuilder(Stats);
            builder[Stat.Damage] *= scale;
            builder[Stat.ExplosionDamage] *= scale;
            return WithStats(new SkillStats(builder));
        }

        public SkillConfig WithChildCaster(IChildSkillCaster childCaster) => new SkillConfig(Stats, Attack, Bindings, childCaster, Children, Overlays);

        internal SkillConfig WithOverlays(IReadOnlyDictionary<int, IReadOnlyList<EffectDef>> overlays) => new SkillConfig(Stats, Attack, Bindings, ChildCaster, Children, overlays);

        internal SkillConfig WithStats(SkillStats stats) => new SkillConfig(stats, Attack, Bindings, ChildCaster, Children, Overlays);

        public static SkillConfig FromDefinition(SkillData data) => new SkillConfig(
            SkillStats.FromDefinition(data.baseStats), new AttackDefinition(data.castType, data.projectilePath, data.baseStats.cast.range),
            Array.Empty<ReactionBinding>());
    }

    /// <summary>Builds an immutable configuration. Failed effect batches never change the builder.</summary>
    public sealed class SkillConfigBuilder
    {
        internal SkillStatsBuilder Values { get; private set; }
        private readonly AttackDefinition attack;
        internal IChildSkillCaster ChildCaster { get; }
        private List<ReactionBinding> bindings;
        private Dictionary<int, ChildLink> children;
        private Dictionary<int, IReadOnlyList<EffectDef>> overlays;

        public SkillConfigBuilder(SkillConfig config)
        {
            Values = new SkillStatsBuilder(config.Stats);
            attack = config.Attack;
            ChildCaster = config.ChildCaster;
            bindings = new List<ReactionBinding>(config.Bindings);
            children = new Dictionary<int, ChildLink>(config.Children);
            overlays = new Dictionary<int, IReadOnlyList<EffectDef>>(config.Overlays);
        }

        internal SkillConfigBuilder(SkillStats stats) : this(new SkillConfig(stats, default, Array.Empty<ReactionBinding>())) { }

        public void AddReaction(AttackEvent trigger, IAttackReaction reaction) => bindings.Add(new ReactionBinding(trigger, reaction));

        /// <summary>설정이 만들어질 때 그 설정으로 반응을 만드는 반응을 덧붙인다 (부모의 스탯을 읽는 반응용)</summary>
        internal void AddLateReaction(AttackEvent trigger, Func<SkillConfig, IAttackReaction> factory) =>
            bindings.Add(ReactionBinding.Late(trigger, factory));

        /// <summary>자식 연결을 만들거나 상속 규칙을 더한다. 같은 스탯을 다시 적으면 나중 값이 이긴다.</summary>
        internal void LinkChild(int skillId, IReadOnlyList<ChildLink.Inheritance> inherit)
        {
            children.TryGetValue(skillId, out var link);
            children[skillId] = (link ?? new ChildLink(skillId, Array.Empty<ChildLink.Inheritance>())).WithInheritance(inherit);
        }

        /// <summary>해당 ID의 스킬에 적용할 효과를 보관한다. 그 스킬이 아직 시전되지 않아도 받아 두었다가 시전될 때 적용한다.</summary>
        internal void AddOverlay(int skillId, EffectDef effect)
        {
            var list = overlays.TryGetValue(skillId, out var existing) ? new List<EffectDef>(existing) : new List<EffectDef>();
            list.Add(effect);
            overlays[skillId] = list;
        }

        public bool TryApplyCatalog(IEnumerable<EffectDef> effects)
        {
            if (effects == null) { return false; }
            var compiled = new List<IUpgradeEffect>();
            foreach (var effect in effects)
            {
                if (!SkillStatEffects.TryCompile(effect, out var upgrade)) { return false; }
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
            children = prepared.children;
            overlays = prepared.overlays;
            return true;
        }

        public SkillConfig Build() => new SkillConfig(new SkillStats(Values), attack, bindings, ChildCaster, children, overlays);
    }

    public interface IUpgradeEffect
    {
        bool TryApply(SkillConfigBuilder builder);
    }

    public abstract class NumericUpgradeEffect : IUpgradeEffect
    {
        private readonly string kind;
        private readonly float value;
        private readonly UpgradeEffectCategory category;
        protected NumericUpgradeEffect(string kind, float value, UpgradeEffectCategory category)
        {
            this.kind = kind;
            this.value = value;
            this.category = category;
        }
        public bool TryApply(SkillConfigBuilder builder) => SkillStatEffects.TryApplyValue(builder.Values, kind, value, category);
    }

    public sealed class StatUpgradeEffect : NumericUpgradeEffect
    {
        public StatUpgradeEffect(string kind, float value) : base(kind, value, UpgradeEffectCategory.Stat) { }
    }

    public sealed class CastUpgradeEffect : NumericUpgradeEffect
    {
        public CastUpgradeEffect(string kind, float value) : base(kind, value, UpgradeEffectCategory.Cast) { }
    }

    public sealed class TransformUpgradeEffect : NumericUpgradeEffect
    {
        public TransformUpgradeEffect(SkillForm form) : base("form", (int)form, UpgradeEffectCategory.Transform) { }
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

    // 반응 분류의 숫자 효과는 반응 객체를 만들지 않고 스탯만 켠다. 실제 반응은 ReactionCompiler가 스냅샷에서 만든다.
    // 반응을 직접 정의하려면 ReactionUpgradeEffect를 쓴다.
    internal sealed class ReactionStatUpgradeEffect : NumericUpgradeEffect
    {
        public ReactionStatUpgradeEffect(string kind, float value) : base(kind, value, UpgradeEffectCategory.Reaction) { }
    }
}
