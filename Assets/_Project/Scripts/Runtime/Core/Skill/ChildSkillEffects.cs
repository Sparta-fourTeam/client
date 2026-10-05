using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>이벤트가 난 위치에서 자식 스킬을 시전한다. 자식은 자기 공격과 반응을 직접 가진다.
    /// 구현은 공격 전략 단계에서 연결한다. 연결 전에는 자식 시전 효과가 적용되지 않는다(강화 실패로 드러난다).</summary>
    public interface IChildSkillCaster
    {
        void Cast(int skillId, AttackContext context, float damageScale);
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

        public ChildCastUpgradeEffect(AttackEvent trigger, int skillId, int count, float chance, float damageScale, float interval = 0)
        {
            this.damageScale = damageScale;
            this.trigger = trigger;
            this.skillId = skillId;
            this.count = count;
            this.chance = chance;
            this.interval = interval;
        }

        public bool TryApply(SkillConfigBuilder builder)
        {
            var caster = builder.ChildCaster;
            if (caster == null) { return false; }
            IAttackReaction cast = new CastSkillReaction(c => caster.Cast(skillId, c, damageScale), count, chance);
            if (interval > 0) { cast = new PeriodicReaction(interval, cast); }
            builder.AddReaction(trigger, cast);
            return true;
        }

        public static bool Valid(EffectDef e) =>
            e.skillId > 0 && e.count >= 1 && e.chance > 0 && e.chance <= 1 && !float.IsNaN(e.chance)
            && e.damageScale > 0 && !float.IsNaN(e.damageScale) && !float.IsInfinity(e.damageScale);

        public static IUpgradeEffect FromEvent(EffectDef e) =>
            Valid(e) && e.trigger != AttackEvent.Tick
                ? new ChildCastUpgradeEffect(e.trigger, e.skillId, e.count, e.chance, e.damageScale)
                : null;

        public static IUpgradeEffect FromPeriodic(EffectDef e) =>
            Valid(e) && e.interval > 0 && !float.IsInfinity(e.interval)
                ? new ChildCastUpgradeEffect(AttackEvent.Tick, e.skillId, e.count, e.chance, e.damageScale, e.interval)
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

        public void Cast(int skillId, AttackContext context, float damageScale)
        {
            if (!children.TryGetValue(skillId, out var child))
            {
                child = create(skillId);
                children[skillId] = child;
            }
            child?.FireAt(context.Position, damageScale);
        }

        public void Dispose()
        {
            foreach (var child in children.Values) { child?.Dispose(); }
            children.Clear();
        }
    }
}
