using UnityEngine;
using UnityEngine.Pool;

namespace Game.Core
{
    /// <summary>Creates primary projectiles. 맞았을 때의 피해·상태이상·폭발은 ReactionCompiler가, 분열 같은 자식 시전은 강화 카드의 반응이 맡는다.</summary>
    public sealed class ProjectileSpawnRules
    {
        private readonly WeaponStats stats;
        private readonly IEnemyTargetProvider targetProvider;
        private readonly ObjectPool<Projectile> pool;
        private readonly Vector3 projectileScale;
        private readonly AttackReactions reactions;

        public ProjectileSpawnRules(WeaponStats stats, IEnemyTargetProvider targetProvider,
            ObjectPool<Projectile> pool, Vector3 projectileScale, AttackReactions reactions = null)
        {
            this.stats = stats;
            this.targetProvider = targetProvider;
            this.pool = pool;
            this.projectileScale = projectileScale;
            this.reactions = reactions ?? AttackReactions.Empty;
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
    }
}
