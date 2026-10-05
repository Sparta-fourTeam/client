using UnityEngine;
using UnityEngine.Pool;

namespace Game.Core
{
    /// <summary>Creates primary projectiles and composes their impact callbacks.</summary>
    public sealed class ProjectileSpawnRules
    {
        private readonly WeaponStats stats;
        private readonly WeaponData data;
        private readonly IEnemyTargetProvider targetProvider;
        private readonly ObjectPool<Projectile> pool;
        private readonly Vector3 projectileScale;
        private readonly ProjectileBranchSpawner branches;
        private readonly AttackReactions reactions;

        public ProjectileSpawnRules(WeaponStats stats, WeaponData data, IEnemyTargetProvider targetProvider,
            ObjectPool<Projectile> pool, Vector3 projectileScale, AttackReactions reactions = null)
        {
            this.stats = stats;
            this.data = data;
            this.targetProvider = targetProvider;
            this.pool = pool;
            this.projectileScale = projectileScale;
            this.reactions = reactions ?? AttackReactions.Empty;
            branches = new ProjectileBranchSpawner(this.stats, data, targetProvider, pool, projectileScale);
        }

        public void SpawnMain(Vector3 start, Vector3 direction, float lifetime, int pierce)
        {
            Projectile projectile = pool.Get();
            projectile.transform.localScale = ProjectileVisual.MainScale(projectileScale, stats.Cast.Form, stats.Projectile.SizeMultiplier);
            projectile.SetVisualForm(stats.Cast.Form);
            projectile.Init(pool, new ProjectileSpawnSettings
            {
                StartPos = start,
                Direction = direction,
                Damage = stats.Cast.Damage,
                Speed = stats.Projectile.Speed,
                Lifetime = lifetime,
                TargetProvider = targetProvider,
                Reactions = reactions,
                PierceCount = pierce,
                FreezeDuration = stats.Status.FreezeDuration,
                OnHit = CreateHitCallback(),
                KnockbackDistance = stats.Projectile.KnockbackDistance,
                FrostbiteRatio = stats.Status.FrostbiteRatio,
                ParalysisDuration = stats.Status.ParalysisDuration,
                LightningDamage = stats.Cast.Damage * stats.Secondary.LightningStrikeRatio,
                ParalysisChance = data.baseStats.paralysisChance,
                BurnDuration = stats.Burn.Duration,
                BurnDamage = stats.Cast.Damage * stats.Burn.DamageRatio,
                BurnMaxHpRatio = stats.Burn.MaxHpRatio,
                BurnOnDeath = CreateBurnDeathCallback(),
                FreezeChance = data.baseStats.freezeChance,
                FrostbiteChance = data.baseStats.frostbiteChance,
                BurnChance = data.baseStats.burnChance,
                CollisionRadius = .3f * stats.Projectile.SizeMultiplier,
                StunDuration = stats.Status.StunDuration,
                StunChance = data.baseStats.stunChance,
                SlowDuration = stats.Status.SlowDuration,
                SlowRatio = stats.Status.SlowRatio,
                VulnerabilityRatio = stats.Status.VulnerabilityRatio,
                VulnerabilityDuration = stats.Status.VulnerabilityDuration
            });
        }
        private System.Action<Vector2> CreateBurnDeathCallback()
        {
            if (!stats.Burn.DeathExplosion)
            {
                return null;
            }

            float radius = stats.Explosion.Radius;
            float damage = stats.Explosion.Damage;
            return position => SkillReactionEffects.Explode(targetProvider, position, radius, damage);
        }

        private System.Action<Vector2, Vector3, IEnemyTarget> CreateHitCallback()
        {
            var split = stats.Cast.Form == WeaponForm.TriangleIce ? branches.CreateTriangleCallback() : branches.CreateSplitCallback();
            float radius = stats.Explosion.Radius;
            float damage = stats.Explosion.Damage;
            var bindings = new System.Collections.Generic.List<ReactionBinding>();
            if (radius > 0)
            {
                bindings.Add(new ReactionBinding(AttackEvent.Hit,
                new CastSkillReaction(c => SkillReactionEffects.Explode(targetProvider, c.Position, radius, damage))));
            }
            if (split != null)
            {
                bindings.Add(new ReactionBinding(AttackEvent.Hit,
                new CastSkillReaction(c => split(c.Position, c.Direction, c.Target))));
            }
            if (bindings.Count == 0) { return null; }
            var onHit = new AttackReactions(bindings);
            return (position, direction, target) => onHit.Raise(AttackEvent.Hit, new AttackContext(position, direction, target));
        }

    }
}
