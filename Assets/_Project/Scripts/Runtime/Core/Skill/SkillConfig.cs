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
        /// <summary>자식 스킬 시전 효과가 쓰는 시전기. 연결되지 않았으면 null</summary>
        public IChildSkillCaster ChildCaster { get; }
        /// <summary>시전하는 자식 스킬별 연결(상속 규칙과 자식 전용 강화). 자식 ID로 찾는다</summary>
        public IReadOnlyDictionary<int, ChildLink> Children { get; }
        internal readonly ReactionBinding[] Bindings;

        internal SkillConfig(WeaponStats stats, AttackDefinition attack, IEnumerable<ReactionBinding> bindings,
            IChildSkillCaster childCaster = null, IReadOnlyDictionary<int, ChildLink> children = null)
        {
            Stats = stats;
            Attack = attack;
            ChildCaster = childCaster;
            Children = children ?? new Dictionary<int, ChildLink>();
            Bindings = new List<ReactionBinding>(bindings).ToArray();
            var resolved = new List<ReactionBinding>(Bindings.Length);
            foreach (var binding in Bindings) { resolved.Add(binding.Resolve(this)); }
            Reactions = new AttackReactions(resolved);
        }

        /// <summary>피해(직접·폭발)에 배율을 곱한 설정. 자식 스킬을 소형으로 시전할 때 쓴다</summary>
        public SkillConfig WithDamageScale(float scale)
        {
            var builder = new WeaponStatsBuilder(Stats);
            builder[Stat.Damage] *= scale;
            builder[Stat.ExplosionDamage] *= scale;
            return WithStats(new WeaponStats(builder));
        }

        public SkillConfig WithChildCaster(IChildSkillCaster childCaster) => new SkillConfig(Stats, Attack, Bindings, childCaster, Children);

        internal SkillConfig WithStats(WeaponStats stats) => new SkillConfig(stats, Attack, Bindings, ChildCaster, Children);

        public static SkillConfig FromDefinition(WeaponData data) => new SkillConfig(
            WeaponStats.FromDefinition(data.baseStats), new AttackDefinition(data.castType, data.projectilePath, data.baseStats.cast.range),
            Array.Empty<ReactionBinding>());
    }

    /// <summary>Builds an immutable configuration. Failed effect batches never change the builder.</summary>
    public sealed class SkillConfigBuilder
    {
        internal WeaponStatsBuilder Values { get; private set; }
        private readonly AttackDefinition attack;
        internal IChildSkillCaster ChildCaster { get; }
        private List<ReactionBinding> bindings;
        private Dictionary<int, ChildLink> children;

        public SkillConfigBuilder(SkillConfig config)
        {
            Values = new WeaponStatsBuilder(config.Stats);
            attack = config.Attack;
            ChildCaster = config.ChildCaster;
            bindings = new List<ReactionBinding>(config.Bindings);
            children = new Dictionary<int, ChildLink>(config.Children);
        }

        internal SkillConfigBuilder(WeaponStats stats) : this(new SkillConfig(stats, default, Array.Empty<ReactionBinding>())) { }

        public void AddReaction(AttackEvent trigger, IAttackReaction reaction) => bindings.Add(new ReactionBinding(trigger, reaction));

        /// <summary>설정이 만들어질 때 그 설정으로 반응을 만드는 반응을 덧붙인다 (부모의 스탯을 읽는 반응용)</summary>
        internal void AddLateReaction(AttackEvent trigger, Func<SkillConfig, IAttackReaction> factory) =>
            bindings.Add(ReactionBinding.Late(trigger, factory));

        /// <summary>자식 연결을 만든다. 이미 있으면 상속 규칙이 같거나 비어 있을 때만 받아들인다.</summary>
        internal bool TryLinkChild(int skillId, IReadOnlyList<ChildLink.Inheritance> inherit)
        {
            if (!children.TryGetValue(skillId, out var link))
            {
                children[skillId] = new ChildLink(skillId, inherit ?? Array.Empty<ChildLink.Inheritance>(), Array.Empty<EffectDef>());
                return true;
            }
            return inherit == null || inherit.Count == 0 || link.SameInheritance(inherit);
        }

        /// <summary>이미 연결된 자식에게만 적용되는 강화 효과를 덧붙인다. 연결이 없으면 실패한다.</summary>
        internal bool TryAddChildOverlay(int skillId, EffectDef effect)
        {
            if (!children.TryGetValue(skillId, out var link)) { return false; }
            children[skillId] = link.WithOverlay(effect);
            return true;
        }

        public bool TryApplyCatalog(IEnumerable<EffectDef> effects)
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
            children = prepared.children;
            return true;
        }

        public SkillConfig Build() => new SkillConfig(new WeaponStats(Values), attack, bindings, ChildCaster, children);
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
        public bool TryApply(SkillConfigBuilder builder) => WeaponStatEffects.TryApplyValue(builder.Values, kind, value, category);
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
        public TransformUpgradeEffect(WeaponForm form) : base("form", (int)form, UpgradeEffectCategory.Transform) { }
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
