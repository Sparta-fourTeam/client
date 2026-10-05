using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>이벤트가 난 위치에서 자식 스킬을 시전한다. 자식은 자기 공격과 반응을 직접 가진다.
    /// 연결되기 전에는 자식 시전 효과가 적용되지 않는다(강화 실패로 드러난다).</summary>
    public interface IChildSkillCaster
    {
        void Cast(ChildCast cast, AttackContext context);
    }

    /// <summary>자식 스킬 시전 한 번의 요청. 어떤 자식을, 어떤 부모 스탯 스냅샷과 연결 규칙으로 시전하는지 담는다.</summary>
    public readonly struct ChildCast
    {
        public int SkillId { get; }
        public float DamageScale { get; }
        public ChildLink Link { get; }
        /// <summary>이 시전을 만든 부모 공격의 스탯. 발사 때의 설정이므로 이후 강화는 반영되지 않는다</summary>
        public WeaponStats ParentStats { get; }

        public ChildCast(int skillId, float damageScale, ChildLink link, WeaponStats parentStats)
        {
            SkillId = skillId;
            DamageScale = damageScale;
            Link = link;
            ParentStats = parentStats;
        }

        /// <summary>자식의 기본 설정에 상속, 자식 전용 강화, 피해 배율을 이 순서로 적용한 설정</summary>
        public SkillConfig Resolve(SkillConfig childBase) => Link != null
            ? Link.Resolve(childBase, ParentStats, DamageScale)
            : UnityEngine.Mathf.Approximately(DamageScale, 1) ? childBase : childBase.WithDamageScale(DamageScale);
    }

    /// <summary>
    /// 부모 스킬과 자식 스킬 하나의 연결. 자식 설정은 이렇게 정해진다.
    /// 1) 자식의 기본 설정, 2) 상속: 부모 스탯 × 배율로 덮어쓴다, 3) 자식 전용 강화(target 효과)를 카드를 얻은 순서대로, 4) 시전 지점의 피해 배율.
    /// </summary>
    public sealed class ChildLink
    {
        public readonly struct Inheritance
        {
            internal Stat Stat { get; }
            public float Scale { get; }
            internal Inheritance(Stat stat, float scale) { Stat = stat; Scale = scale; }
        }

        public int SkillId { get; }
        internal IReadOnlyList<Inheritance> Inherit { get; }
        internal IReadOnlyList<EffectDef> Overlay { get; }

        internal ChildLink(int skillId, IReadOnlyList<Inheritance> inherit, IReadOnlyList<EffectDef> overlay)
        {
            SkillId = skillId;
            Inherit = inherit;
            Overlay = overlay;
        }

        internal ChildLink WithOverlay(EffectDef effect)
        {
            var next = new List<EffectDef>(Overlay) { effect };
            return new ChildLink(SkillId, Inherit, next);
        }

        internal bool SameInheritance(IReadOnlyList<Inheritance> other)
        {
            if (other.Count != Inherit.Count) { return false; }
            for (int i = 0; i < other.Count; i++)
            {
                if (other[i].Stat != Inherit[i].Stat || other[i].Scale != Inherit[i].Scale) { return false; }
            }
            return true;
        }

        /// <summary>자식의 기본 설정에 연결 규칙을 적용한다. 원본 설정은 바뀌지 않는다.</summary>
        public SkillConfig Resolve(SkillConfig childBase, WeaponStats parent, float damageScale)
        {
            var values = new WeaponStatsBuilder(childBase.Stats);
            foreach (var rule in Inherit) { values[rule.Stat] = parent.Get(rule.Stat) * rule.Scale; }
            foreach (var effect in Overlay)
            {
                // 연결할 때 이미 검증한 효과라 실패하지 않는다.
                if (EffectRegistry.TryGet(effect.kind, out var kind)) { WeaponStatEffects.TryApplyValue(values, effect.kind, effect.value, kind.Category); }
            }
            if (!UnityEngine.Mathf.Approximately(damageScale, 1))
            {
                values[Stat.Damage] *= damageScale;
                values[Stat.ExplosionDamage] *= damageScale;
            }
            return childBase.WithStats(new WeaponStats(values));
        }

        internal static bool TryParseInherit(Dictionary<string, float> source, out List<Inheritance> inherit)
        {
            inherit = new List<Inheritance>();
            if (source == null) { return true; }
            foreach (var pair in source)
            {
                if (!WeaponStatRegistry.TryParse(pair.Key, out var stat) || !(pair.Value > 0) || float.IsInfinity(pair.Value))
                {
                    return false;
                }
                inherit.Add(new Inheritance(stat, pair.Value));
            }
            inherit.Sort((a, b) => a.Stat.CompareTo(b.Stat));
            return true;
        }
    }

    /// <summary>onEvent·periodic 효과: 지정한 시점에 자식 스킬을 시전하는 반응을 덧붙인다.</summary>
    internal sealed class ChildCastUpgradeEffect : IUpgradeEffect
    {
        private readonly AttackEvent trigger;
        private readonly int skillId;
        private readonly int count;
        private readonly float chance;
        private readonly float interval;
        private readonly float damageScale;
        private readonly List<ChildLink.Inheritance> inherit;

        public ChildCastUpgradeEffect(AttackEvent trigger, int skillId, int count, float chance, float damageScale,
            List<ChildLink.Inheritance> inherit, float interval = 0)
        {
            this.damageScale = damageScale;
            this.trigger = trigger;
            this.skillId = skillId;
            this.count = count;
            this.chance = chance;
            this.interval = interval;
            this.inherit = inherit;
        }

        public bool TryApply(SkillConfigBuilder builder)
        {
            var caster = builder.ChildCaster;
            if (caster == null || !builder.TryLinkChild(skillId, inherit)) { return false; }
            // 부모의 스탯 스냅샷은 설정이 만들어질 때 정해진다. 이미 발사된 공격은 발사 때의 값을 쓴다.
            builder.AddLateReaction(trigger, config =>
            {
                var link = config.Children[skillId];
                var stats = config.Stats;
                IAttackReaction cast = new CastSkillReaction(c => caster.Cast(new ChildCast(skillId, damageScale, link, stats), c), count, chance);
                return interval > 0 ? new PeriodicReaction(interval, cast) : cast;
            });
            return true;
        }

        public static bool Valid(EffectDef e, out List<ChildLink.Inheritance> inherit)
        {
            inherit = null;
            return e.skillId > 0 && e.count >= 1 && e.chance > 0 && e.chance <= 1 && !float.IsNaN(e.chance)
                && e.damageScale > 0 && !float.IsNaN(e.damageScale) && !float.IsInfinity(e.damageScale)
                && e.target == 0 && ChildLink.TryParseInherit(e.inherit, out inherit);
        }

        public static IUpgradeEffect FromEvent(EffectDef e) =>
            Valid(e, out var inherit) && e.trigger != AttackEvent.Tick
                ? new ChildCastUpgradeEffect(e.trigger, e.skillId, e.count, e.chance, e.damageScale, inherit)
                : null;

        public static IUpgradeEffect FromPeriodic(EffectDef e) =>
            Valid(e, out var inherit) && e.interval > 0 && !float.IsInfinity(e.interval)
                ? new ChildCastUpgradeEffect(AttackEvent.Tick, e.skillId, e.count, e.chance, e.damageScale, inherit, e.interval)
                : null;
    }

    /// <summary>target이 있는 스탯 효과: 부모가 아니라 연결된 자식에게만 적용되는 강화 (예: 분열 조각 피해 증폭).</summary>
    internal sealed class ChildOverlayUpgradeEffect : IUpgradeEffect
    {
        private readonly EffectDef effect;
        public ChildOverlayUpgradeEffect(EffectDef effect) => this.effect = effect;

        public bool TryApply(SkillConfigBuilder builder) => builder.TryAddChildOverlay(effect.target, effect);

        public static IUpgradeEffect From(EffectDef e) =>
            e.target > 0 && EffectRegistry.IsStatKind(e.kind) && EffectRegistry.TryGet(e.kind, out var kind) && kind.Accept(e.value)
                ? new ChildOverlayUpgradeEffect(e)
                : null;
    }

    /// <summary>자식 스킬을 이벤트가 난 위치에서 시전한다. 자식은 처음 필요할 때 한 번 만들어 재사용하며,
    /// 쿨타임과 무관하게 시전 요청마다 공격을 낸다. 자식 시전의 순환은 카탈로그 검증이 막는다.</summary>
    public sealed class ChildSkillCaster : IChildSkillCaster, IDisposable
    {
        private readonly Func<int, SkillCaster> create;
        private readonly Dictionary<int, SkillCaster> children = new Dictionary<int, SkillCaster>();

        /// <param name="create">스킬 ID로 시전기를 만든다. 만들 수 없으면 null (사유는 만드는 쪽이 알린다)</param>
        public ChildSkillCaster(Func<int, SkillCaster> create) => this.create = create;

        public void Cast(ChildCast cast, AttackContext context)
        {
            if (!children.TryGetValue(cast.SkillId, out var child))
            {
                child = create(cast.SkillId);
                children[cast.SkillId] = child;
            }
            child?.FireAt(context.Position, cast.Resolve);
        }

        public void Dispose()
        {
            foreach (var child in children.Values) { child?.Dispose(); }
            children.Clear();
        }
    }
}
