using UnityEngine;
using UnityEngine.Pool;

namespace Game.Core
{
    /// <summary>Creates primary projectiles and composes their impact callbacks.</summary>
    public sealed class ProjectileSpawnRules
    {
        private readonly WeaponStats stats;
        private readonly IEnemyTargetProvider targetProvider;
        private readonly ObjectPool<Projectile> pool;
        private readonly Vector3 projectileScale;
        private readonly ProjectileBranchSpawner branches;
        private readonly AttackReactions reactions;

        public ProjectileSpawnRules(WeaponStats stats, IEnemyTargetProvider targetProvider,
            ObjectPool<Projectile> pool, Vector3 projectileScale, AttackReactions reactions = null)
        {
            this.stats = stats;
            this.targetProvider = targetProvider;
            this.pool = pool;
            this.projectileScale = projectileScale;
            this.reactions = reactions ?? AttackReactions.Empty;
            branches = new ProjectileBranchSpawner(this.stats, targetProvider, pool, projectileScale);
        }

        public void SpawnMain(Vector3 start, Vector3 direction, float lifetime, int pierce, IEnemyTarget ignoredTarget = null)
        {
            Projectile projectile = pool.Get();
            projectile.transform.localScale = ProjectileVisual.MainScale(projectileScale, stats.Cast.Form, stats.Projectile.SizeMultiplier);
            projectile.SetVisualForm(stats.Cast.Form);
            projectile.Init(pool, new ProjectileSpawnSettings
            {
                StartPos = start,
                Direction = direction,
                Speed = stats.Projectile.Speed,
                Lifetime = lifetime,
                TargetProvider = targetProvider,
                Reactions = reactions,
                PierceCount = pierce,
                IgnoredTarget = ignoredTarget,
                HitReactions = ReactionCompiler.ForProjectile(stats, CreateBurnDeathCallback(), targetProvider),
                OnHit = CreateHitCallback(),
                CollisionRadius = .3f * stats.Projectile.SizeMultiplier
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
            var split = branches.CreateSplitCallback();
            var bindings = new System.Collections.Generic.List<ReactionBinding>();
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
