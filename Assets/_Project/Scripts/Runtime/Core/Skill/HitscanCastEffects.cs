using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace Game.Core
{
    /// <summary>Builds cast callbacks from fixed values; active casts keep their original upgrades.</summary>
    public sealed class HitscanCastEffects
    {
        private readonly WeaponStats stats;
        private readonly float range;
        private readonly IEnemyTargetProvider targets;
        private readonly ObjectPool<HitscanEffect> strikes;
        private readonly ObjectPool<Projectile> orbs;
        private readonly ObjectPool<ElectromagneticField> fields;
        private readonly Vector3 scale;
        private readonly AttackReactions reactions;

        public HitscanCastEffects(WeaponStats stats, float range, IEnemyTargetProvider targets,
            ObjectPool<HitscanEffect> strikes, ObjectPool<Projectile> orbs,
            ObjectPool<ElectromagneticField> fields, Vector3 scale, AttackReactions reactions = null)
        {
            this.stats = stats;
            this.range = range;
            this.targets = targets;
            this.strikes = strikes;
            this.orbs = orbs;
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
            strike.transform.localScale = stats.Cast.Form == WeaponForm.JudgementThunder ? scale * 1.5f : scale;
            strike.Init(strikes, new Vector3(target.Position.x, target.Position.y, 0), stats.Cast.Damage,
                reactions: CompileReactions(target).Then(reactions), sourceTarget: target);
        }

        // 맞은 적마다 마비(Hit), 목표 지점에 한 번 폭발과 번개 구체(Impact), 처치마다 처치 번개(Kill)
        private AttackReactions CompileReactions(IEnemyTarget target)
        {
            var builder = new HitReactionBuilder().Paralysis(stats.Status.ParalysisDuration, stats.Status.ParalysisChance);
            builder.Explosion(AttackEvent.Impact, targets, stats.Explosion.Radius, stats.Explosion.Damage);
            var orbs = LightningOrbSpawner.CreateCallback(stats, targets, this.orbs, target);
            if (orbs != null) { builder.On(AttackEvent.Impact, new CastSkillReaction(c => orbs(c.Position))); }
            var killLightning = CreateKillLightningCallback();
            if (killLightning != null) { builder.On(AttackEvent.Kill, new CastSkillReaction(c => killLightning(c.Position))); }
            return builder.Build();
        }

        public Action<Vector2> CreateKillLightningCallback()
        {
            float damage = stats.Cast.Damage * stats.Secondary.KillLightningRatio;
            if (damage <= 0) { return null; }
            return position =>
            {
                var candidates = new List<IEnemyTarget>();
                targets.GetNearest(position, int.MaxValue, candidates);
                IEnemyTarget nearest = null;
                float nearestDistance = range * range;
                foreach (var candidate in candidates)
                {
                    if (candidate == null || candidate is EnemyModel model && model.IsDead) { continue; }
                    float distance = (candidate.Position - position).sqrMagnitude;
                    if (distance <= nearestDistance) { nearest = candidate; nearestDistance = distance; }
                }
                if (nearest == null) { return; }
                var secondary = strikes.Get();
                secondary.SetVisualForm(WeaponForm.Default);
                secondary.transform.localScale = scale * .5f;
                secondary.Init(strikes, nearest.Position, damage, directTarget: nearest);
            };
        }
    }
}
