using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace Game.Core
{
    /// <summary>Builds cast callbacks from fixed values; active casts keep their original upgrades.</summary>
    public sealed class HitscanCastEffects
    {
        private readonly SkillStats stats;
        private readonly float range;
        private readonly IEnemyTargetProvider targets;
        private readonly ObjectPool<HitscanEffect> strikes;
        private readonly ObjectPool<ElectromagneticField> fields;
        private readonly Vector3 scale;
        private readonly AttackReactions reactions;

        public HitscanCastEffects(SkillStats stats, float range, IEnemyTargetProvider targets,
            ObjectPool<HitscanEffect> strikes,
            ObjectPool<ElectromagneticField> fields, Vector3 scale, AttackReactions reactions = null)
        {
            this.stats = stats;
            this.range = range;
            this.targets = targets;
            this.strikes = strikes;
            this.fields = fields;
            this.scale = scale;
            this.reactions = reactions ?? AttackReactions.Empty;
        }

        public void Cast(IEnemyTarget target)
        {
            var field = stats.Field;
            if (field.Duration > 0)
            {
                float damage = (stats.Cast.Damage * field.DamageRatio + field.FlatDamage) * field.DamageMultiplier;
                fields.Get().Init(fields, targets, target.Position, field.Radius, damage, field.Duration, field.SlowRatio);
            }
            var strike = strikes.Get();
            strike.SetVisualForm(stats.Cast.Form);
            strike.transform.localScale = stats.Cast.Form == SkillForm.JudgementThunder ? scale * 1.5f : scale;
            strike.Init(strikes, new Vector3(target.Position.x, target.Position.y, 0), stats.Cast.Damage,
                reactions: CompileReactions().Then(reactions), sourceTarget: target);
        }

        // 맞은 적마다 마비(Hit), 목표 지점에 한 번 폭발(Impact), 처치마다 처치 번개(Kill). 번개 구체 같은 자식 시전은 강화 카드의 반응이 맡는다.
        private AttackReactions CompileReactions()
        {
            var builder = new HitReactionBuilder().Paralysis(stats.Status.ParalysisDuration, stats.Status.ParalysisChance);
            builder.Explosion(AttackEvent.Impact, targets, stats.Explosion.Radius, stats.Explosion.Damage);
            var killLightning = CreateKillLightningCallback();
            if (killLightning != null) { builder.On(AttackEvent.Kill, new CastSkillReaction(c => killLightning(c.Position))); }
            return builder.Build();
        }

        public Action<Vector2> CreateKillLightningCallback()
        {
            float damage = stats.Cast.Damage * stats.Lightning.KillRatio;
            if (damage <= 0) { return null; }
            return position =>
            {
                var candidates = new List<IEnemyTarget>();
                targets.GetNearest(position, int.MaxValue, candidates);
                IEnemyTarget nearest = null;
                float nearestDistance = range * range;
                foreach (var candidate in candidates)
                {
                    if (candidate == null || candidate.IsDead) { continue; }
                    float distance = (candidate.Position - position).sqrMagnitude;
                    if (distance <= nearestDistance) { nearest = candidate; nearestDistance = distance; }
                }
                if (nearest == null) { return; }
                var secondary = strikes.Get();
                secondary.SetVisualForm(SkillForm.Default);
                secondary.transform.localScale = scale * .5f;
                secondary.Init(strikes, nearest.Position, damage, directTarget: nearest);
            };
        }
    }
}
