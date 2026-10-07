using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>적중 반응을 정해진 순서로 쌓는다. 값이 꺼짐(0 이하)이면 반응을 만들지 않는다.
    /// 스탯에서 만드는 경로(<see cref="ReactionCompiler"/>)와 값을 직접 정하는 보조 투사체가 같은 생성 규칙을 쓴다.</summary>
    public sealed class HitReactionBuilder
    {
        private readonly List<ReactionBinding> bindings = new List<ReactionBinding>();

        private HitReactionBuilder Add(IAttackReaction reaction, AttackEvent trigger = AttackEvent.Hit)
        {
            bindings.Add(new ReactionBinding(trigger, reaction));
            return this;
        }

        /// <summary>공격이 적중해 주는 직접 피해(충격)</summary>
        public HitReactionBuilder Damage(float damage) => Add(new DamageReaction(damage, isImpact: true));

        public HitReactionBuilder Freeze(float duration, float chance = 1) =>
            duration > 0 ? Add(new StatusReaction<IFreezableTarget>(chance, (t, _) => t.ApplyFreeze(duration))) : this;

        public HitReactionBuilder Knockback(float distance) =>
            distance > 0 ? Add(new StatusReaction<IKnockbackTarget>(1, (t, c) => t.ApplyKnockback(c.Direction, distance))) : this;

        public HitReactionBuilder Frostbite(float damage, float chance = 1) =>
            damage > 0 ? Add(new StatusReaction<IFrostbiteTarget>(chance, (t, _) => t.ApplyFrostbite(damage))) : this;

        public HitReactionBuilder Paralysis(float duration, float chance = 1) =>
            duration > 0 ? Add(new StatusReaction<IParalyzableTarget>(chance, (t, _) => t.ApplyParalysis(duration))) : this;

        /// <summary>추가 번개: 최소 1 피해를 주고 번개 연출을 낸다</summary>
        public HitReactionBuilder LightningStrike(float damage)
        {
            if (damage <= 0) { return this; }
            Add(new DamageReaction(damage, minimumOne: true));
            return Add(new CastSkillReaction(c => SkillReactionEffects.Lightning(c.Position)));
        }

        public HitReactionBuilder Burn(float damage, float duration, float maxHpRatio, float chance = 1, Action<Vector2> onDeath = null) =>
            damage > 0 && duration > 0
                ? Add(new StatusReaction<IBurnableTarget>(chance, (t, _) => t.ApplyBurn(damage, duration, maxHpRatio, onDeath)))
                : this;

        public HitReactionBuilder Stun(float duration, float chance = 1) =>
            duration > 0 ? Add(new StatusReaction<IStunnableTarget>(chance, (t, _) => t.ApplyStun(duration))) : this;

        public HitReactionBuilder Slow(float ratio, float duration) =>
            ratio > 0 && duration > 0 ? Add(new StatusReaction<ISlowableTarget>(1, (t, _) => t.ApplySlow(ratio, duration))) : this;

        public HitReactionBuilder Vulnerability(float ratio, float duration) =>
            ratio > 0 && duration > 0 ? Add(new StatusReaction<IVulnerableTarget>(1, (t, _) => t.ApplyVulnerability(ratio, duration))) : this;

        /// <summary>그 시점의 위치에서 범위 폭발을 낸다. 반경이 0이면 만들지 않는다. 투사체는 적중(Hit), Hitscan은 타격 지점(Impact)에 건다.</summary>
        public HitReactionBuilder Explosion(AttackEvent trigger, IEnemyTargetProvider provider, float radius, float damage) =>
            radius > 0 && provider != null
                ? Add(new CastSkillReaction(c => SkillReactionEffects.Explode(provider, c.Position, radius, damage)), trigger)
                : this;

        /// <summary>임의 반응을 지정한 시점에 덧붙인다</summary>
        public HitReactionBuilder On(AttackEvent trigger, IAttackReaction reaction) => Add(reaction, trigger);

        public AttackReactions Build() => new AttackReactions(bindings);
    }

    /// <summary>스킬 스탯 스냅샷을 공격 반응으로 바꾸는 유일한 곳. 투사체와 Hitscan이 함께 쓴다.</summary>
    public static class ReactionCompiler
    {
        /// <summary>투사체 적중 반응: 피해와 스탯이 켠 모든 상태이상</summary>
        public static AttackReactions ForProjectile(SkillStats stats, Action<Vector2> burnOnDeath = null, IEnemyTargetProvider explosionTargets = null) =>
            Compile(stats, burnOnDeath, explosionTargets, AttackEvent.Hit);

        /// <summary>연쇄 적중 반응: 투사체와 같되 폭발은 적중마다가 아니라 튕겨 도착할 때(Bounce)마다 난다</summary>
        public static AttackReactions ForChain(SkillStats stats, IEnemyTargetProvider explosionTargets) =>
            Compile(stats, null, explosionTargets, AttackEvent.Bounce);

        private static AttackReactions Compile(SkillStats stats, Action<Vector2> burnOnDeath, IEnemyTargetProvider explosionTargets, AttackEvent explosionTrigger)
        {
            float damage = stats.Cast.Damage;
            var status = stats.Status;
            var burn = stats.Burn;
            return new HitReactionBuilder()
                .Damage(damage)
                .Freeze(status.FreezeDuration, status.FreezeChance)
                .Knockback(stats.Projectile.KnockbackDistance)
                .Frostbite(damage * status.FrostbiteRatio, status.FrostbiteChance)
                .Paralysis(status.ParalysisDuration, status.ParalysisChance)
                .LightningStrike(damage * stats.Lightning.StrikeRatio)
                .Burn(damage * burn.DamageRatio, burn.Duration, burn.MaxHpRatio, burn.Chance, burnOnDeath)
                .Stun(status.StunDuration, status.StunChance)
                .Slow(status.SlowRatio, status.SlowDuration)
                .Vulnerability(status.VulnerabilityRatio, status.VulnerabilityDuration)
                .Explosion(explosionTrigger, explosionTargets, stats.Explosion.Radius, stats.Explosion.Damage)
                .Build();
        }
    }
}
