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
        private readonly WeaponBaseStats definition;
        private readonly IEnemyTargetProvider targets;
        private readonly ObjectPool<HitscanEffect> strikes;
        private readonly ObjectPool<Projectile> orbs;
        private readonly ObjectPool<ElectromagneticField> fields;
        private readonly Vector3 scale;
        private readonly AttackReactions reactions;

        public HitscanCastEffects(WeaponStats stats, WeaponBaseStats definition, IEnemyTargetProvider targets,
            ObjectPool<HitscanEffect> strikes, ObjectPool<Projectile> orbs,
            ObjectPool<ElectromagneticField> fields, Vector3 scale, AttackReactions reactions = null)
        {
            this.stats = stats;
            this.definition = definition;
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
            float radius = stats.Explosion.Radius;
            float explosionDamage = stats.Explosion.Damage;
            Action<Vector2> onHit = radius > 0
                ? position => SkillReactionEffects.Explode(targets, position, radius, explosionDamage)
                : null;
            onHit += LightningOrbSpawner.CreateCallback(stats, definition, targets, orbs, target);
            float duration = stats.Status.ParalysisDuration;
            float chance = definition.paralysisChance;
            Action<Enemy> onTargetHit = duration > 0
                ? enemy => { if (StatusProc.Roll(chance)) { enemy.ApplyParalysis(duration); } }
            : null;
            strike.Init(strikes, new Vector3(target.Position.x, target.Position.y, 0), stats.Cast.Damage,
                onHit, onTargetHit, CreateKillLightningCallback(), reactions: reactions);
        }

        public Action<Vector2> CreateKillLightningCallback()
        {
            float damage = stats.Cast.Damage * stats.Secondary.KillLightningRatio;
            if (damage <= 0) { return null; }
            float range = definition.range;
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
