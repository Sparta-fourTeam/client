using System;
using System.Collections.Generic;
using UnityEngine;

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
        /// <summary>자식이 한 번의 시전에서 쏘는 수. 1이면 자식의 기본 발사 수를 쓴다</summary>
        public int Count { get; }
        /// <summary>자식이 방금 맞은 적을 노리지 않고 지나친다</summary>
        public bool ExcludeHit { get; }
        public ChildLink Link { get; }
        /// <summary>이 시전을 만든 부모 공격의 스탯. 발사 때의 설정이므로 이후 강화는 반영되지 않는다</summary>
        public SkillStats ParentStats { get; }
        /// <summary>스킬 ID별 보관 효과. 자식과 그 후손이 시전될 때 적용한다</summary>
        public IReadOnlyDictionary<int, IReadOnlyList<EffectDef>> Overlays { get; }

        public ChildCast(int skillId, float damageScale, ChildLink link, SkillStats parentStats, int count = 1, bool excludeHit = false,
            IReadOnlyDictionary<int, IReadOnlyList<EffectDef>> overlays = null)
        {
            SkillId = skillId;
            DamageScale = damageScale;
            Count = count;
            ExcludeHit = excludeHit;
            Link = link;
            ParentStats = parentStats;
            Overlays = overlays;
        }

        /// <summary>
        /// 자식의 기본 설정에서 이번 시전의 설정을 만든다. 순서는 상속 → 발사 수 → 피해 배율 → 보관 효과(카드를 얻은 순서)다.
        /// 보관 효과가 자식에게 반응을 붙이면 그 반응은 이 설정의 스탯을 부모로 삼는다.
        /// </summary>
        public SkillConfig Resolve(SkillConfig childBase, IChildSkillCaster caster = null)
        {
            var values = new SkillStatsBuilder(childBase.Stats);
            if (Link != null)
            {
                foreach (var rule in Link.Inherit) { values[rule.Stat] = ParentStats.Get(rule.Stat) * rule.Scale; }
            }
            if (Count > 1) { values[Stat.ProjectileCount] = Count; }
            var config = childBase.WithStats(new SkillStats(values)).WithChildCaster(caster ?? childBase.ChildCaster)
                .WithOverlays(Overlays ?? new Dictionary<int, IReadOnlyList<EffectDef>>());
            if (!Mathf.Approximately(DamageScale, 1)) { config = config.WithDamageScale(DamageScale); }
            if (Overlays != null && Overlays.TryGetValue(SkillId, out var effects) && effects.Count > 0)
            {
                var builder = new SkillConfigBuilder(config);
                if (builder.TryApplyCatalog(effects)) { config = builder.Build(); }
                else { Debug.LogWarning($"[ChildCast] 스킬 {SkillId}에 보관된 효과를 적용하지 못했습니다. 시전기 연결을 확인하세요."); }
            }
            return config;
        }
    }

    /// <summary>부모 스킬과 자식 스킬 하나의 연결: 부모 스탯을 어떻게 가져올지(상속 규칙).</summary>
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

        internal ChildLink(int skillId, IReadOnlyList<Inheritance> inherit)
        {
            SkillId = skillId;
            Inherit = inherit;
        }

        /// <summary>규칙을 더한 새 연결. 같은 스탯이 이미 있으면 새 값으로 바꾼다.</summary>
        internal ChildLink WithInheritance(IReadOnlyList<Inheritance> rules)
        {
            if (rules == null || rules.Count == 0) { return this; }
            var merged = new List<Inheritance>(Inherit);
            foreach (var rule in rules)
            {
                int index = merged.FindIndex(r => r.Stat == rule.Stat);
                if (index >= 0) { merged[index] = rule; } else { merged.Add(rule); }
            }
            merged.Sort((a, b) => a.Stat.CompareTo(b.Stat));
            return new ChildLink(SkillId, merged);
        }

        internal static bool TryParseInherit(Dictionary<string, float> source, out List<Inheritance> inherit)
        {
            inherit = new List<Inheritance>();
            if (source == null) { return true; }
            foreach (var pair in source)
            {
                if (!SkillStatRegistry.TryParse(pair.Key, out var stat) || !(pair.Value > 0) || float.IsInfinity(pair.Value))
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
        private readonly bool excludeHit;
        private readonly SkillForm? onlyForm;
        private readonly List<ChildLink.Inheritance> inherit;

        public ChildCastUpgradeEffect(AttackEvent trigger, int skillId, int count, float chance, float damageScale,
            List<ChildLink.Inheritance> inherit, bool excludeHit, SkillForm? onlyForm, float interval = 0)
        {
            this.excludeHit = excludeHit;
            this.onlyForm = onlyForm;
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
            if (caster == null) { return false; }
            builder.LinkChild(skillId, inherit);
            // 부모의 스탯 스냅샷은 설정이 만들어질 때 정해진다. 이미 발사된 공격은 발사 때의 값을 쓴다.
            builder.AddLateReaction(trigger, config =>
            {
                if (onlyForm.HasValue && config.Stats.Cast.Form != onlyForm.Value) { return null; }
                var link = config.Children[skillId];
                var stats = config.Stats;
                var overlays = config.Overlays;
                IAttackReaction cast = new CastSkillReaction(
                    c => caster.Cast(new ChildCast(skillId, damageScale, link, stats, count, excludeHit, overlays), c), 1, chance);
                return interval > 0 ? new PeriodicReaction(interval, cast) : cast;
            });
            return true;
        }

        public static bool Valid(EffectDef e, out List<ChildLink.Inheritance> inherit, out SkillForm? onlyForm)
        {
            inherit = null;
            onlyForm = null;
            if (!string.IsNullOrEmpty(e.onlyForm))
            {
                if (!Enum.TryParse<SkillForm>(e.onlyForm, true, out var form)) { return false; }
                onlyForm = form;
            }
            return e.skillId > 0 && e.count >= 1 && e.chance > 0 && e.chance <= 1 && !float.IsNaN(e.chance)
                && e.damageScale > 0 && !float.IsNaN(e.damageScale) && !float.IsInfinity(e.damageScale)
                && e.target == 0 && ChildLink.TryParseInherit(e.inherit, out inherit);
        }

        public static IUpgradeEffect FromEvent(EffectDef e) =>
            Valid(e, out var inherit, out var form) && e.trigger != AttackEvent.Tick
                ? new ChildCastUpgradeEffect(e.trigger, e.skillId, e.count, e.chance, e.damageScale, inherit, e.excludeHit, form)
                : null;

        public static IUpgradeEffect FromPeriodic(EffectDef e) =>
            Valid(e, out var inherit, out var form) && e.interval > 0 && !float.IsInfinity(e.interval)
                ? new ChildCastUpgradeEffect(AttackEvent.Tick, e.skillId, e.count, e.chance, e.damageScale, inherit, e.excludeHit, form, e.interval)
                : null;
    }

    /// <summary>inherit 효과: skillId 자식이 부모에게서 가져오는 스탯 규칙을 더한다. 자식 연결이 아직 없어도 연결을 만들어 둔다.</summary>
    internal sealed class ChildInheritUpgradeEffect : IUpgradeEffect
    {
        private readonly int skillId;
        private readonly List<ChildLink.Inheritance> rules;
        private ChildInheritUpgradeEffect(int skillId, List<ChildLink.Inheritance> rules) { this.skillId = skillId; this.rules = rules; }

        public bool TryApply(SkillConfigBuilder builder)
        {
            builder.LinkChild(skillId, rules);
            return true;
        }

        public static IUpgradeEffect From(EffectDef e) =>
            e.skillId > 0 && e.target == 0 && e.inherit != null && e.inherit.Count > 0 && ChildLink.TryParseInherit(e.inherit, out var rules)
                ? new ChildInheritUpgradeEffect(e.skillId, rules)
                : null;
    }

    /// <summary>target이 있는 효과: 부모가 아니라 해당 ID의 스킬에 적용할 효과로 보관한다.
    /// 그 스킬이 시전될 때(어느 단계든, 연결 순서와 무관하게) 그 스킬의 설정에 적용된다.</summary>
    internal sealed class ChildOverlayUpgradeEffect : IUpgradeEffect
    {
        private readonly int target;
        private readonly EffectDef inner;
        private ChildOverlayUpgradeEffect(int target, EffectDef inner) { this.target = target; this.inner = inner; }

        public bool TryApply(SkillConfigBuilder builder)
        {
            builder.AddOverlay(target, inner);
            return true;
        }

        public static IUpgradeEffect From(EffectDef e)
        {
            if (e.target <= 0 || !EffectRegistry.TryGet(e.kind, out var kind)) { return null; }
            // 보관할 때는 target을 뗀 사본으로 만들어, 대상 스킬의 설정에 적용될 때 그 스킬 자신의 효과로 동작하게 한다.
            var inner = new EffectDef
            {
                kind = e.kind,
                value = e.value,
                trigger = e.trigger,
                skillId = e.skillId,
                inherit = e.inherit,
                chance = e.chance,
                count = e.count,
                interval = e.interval,
                damageScale = e.damageScale,
                excludeHit = e.excludeHit,
                onlyForm = e.onlyForm,
                target = 0
            };
            if (kind.Compile != null) { return kind.Compile(inner) != null ? new ChildOverlayUpgradeEffect(e.target, inner) : null; }
            return EffectRegistry.IsStatKind(e.kind) && kind.Accept(e.value) ? new ChildOverlayUpgradeEffect(e.target, inner) : null;
        }
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
            child?.FireAt(context.Position, config => cast.Resolve(config, this), cast.ExcludeHit ? context.Target : null,
                cast.ExcludeHit ? context.Direction : default);
        }

        public void Dispose()
        {
            foreach (var child in children.Values) { child?.Dispose(); }
            children.Clear();
        }
    }
}
