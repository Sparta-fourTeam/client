using UnityEngine;
using UnityEngine.Pool;

namespace Game.Core
{
    /// <summary>Creates triangle branches and shards from the cast snapshot.</summary>
    public sealed class ProjectileBranchSpawner
    {
        private readonly WeaponStats stats;
        private readonly IEnemyTargetProvider targetProvider;
        private readonly ObjectPool<Projectile> pool;
        private readonly Vector3 projectileScale;
        private const float ProjectileLifetime = 3f;

        public ProjectileBranchSpawner(WeaponStats stats, IEnemyTargetProvider targetProvider,
            ObjectPool<Projectile> pool, Vector3 projectileScale)
        {
            this.stats = stats;
            this.targetProvider = targetProvider;
            this.pool = pool;
            this.projectileScale = projectileScale;
        }

        public System.Action<Vector2, Vector3, IEnemyTarget> CreateTriangleCallback()
        {
            float damage = stats.Cast.Damage * 0.5f;
            float speed = stats.Projectile.Speed;
            int pierce = stats.Projectile.PierceCount;
            float freeze = stats.Status.FreezeDuration;
            float knockback = stats.Projectile.KnockbackDistance;
            float frostbite = stats.Status.FrostbiteRatio;
            var normalSplit = CreateSplitCallbackForDamage(damage);
            float freezeChance = stats.Status.FreezeChance;
            float frostbiteChance = stats.Status.FrostbiteChance;
            return (position, direction, source) =>
            {
                for (int i = 0; i < 3; i++)
                {
                    var normal = pool.Get();
                    normal.transform.localScale = projectileScale;
                    normal.SetVisualForm(WeaponForm.Default);
                    normal.Init(pool, new ProjectileSpawnSettings
                    {
                        StartPos = position,
                        Direction = Quaternion.Euler(0, 0, -30 + i * 30) * direction,
                        Speed = speed,
                        Lifetime = ProjectileLifetime,
                        TargetProvider = targetProvider,
                        PierceCount = pierce,
                        OnHit = normalSplit,
                        IgnoredTarget = source,
                        HitReactions = new HitReactionBuilder()
                            .Damage(damage)
                            .Freeze(freeze, freezeChance)
                            .Knockback(knockback)
                            .Frostbite(damage * frostbite, frostbiteChance)
                            .Build()
                    });
                }
            };
        }

        public System.Action<Vector2, Vector3, IEnemyTarget> CreateSplitCallback() => CreateSplitCallbackForDamage(stats.Cast.Damage);

        private System.Action<Vector2, Vector3, IEnemyTarget> CreateSplitCallbackForDamage(float sourceDamage)
        {
            int count = stats.Secondary.Count;
            if (count <= 0)
            {
                return null;
            }

            float shardDamage = sourceDamage * 0.5f * stats.Secondary.DamageMultiplier;
            float shardSpeed = stats.Projectile.Speed;
            float shardFrostbite = stats.Secondary.FrostbiteRatio;
            float auxiliaryLightningDamage = shardDamage * stats.Secondary.LightningRatio;
            float auxiliaryExplosionRadius = stats.Secondary.Explosions ? stats.Explosion.Radius : 0;
            float auxiliaryExplosionDamage = stats.Explosion.Damage * 0.5f * stats.Secondary.DamageMultiplier;
            System.Action<Vector2, Vector3, IEnemyTarget> auxiliaryHit = auxiliaryExplosionRadius > 0
                ? (position, direction, target) => SkillReactionEffects.Explode(targetProvider, position, auxiliaryExplosionRadius, auxiliaryExplosionDamage)
                : null;
            return (position, direction, target) =>
            {
                for (int i = 0; i < count; i++)
                {
                    float angle = count == 1 ? 0 : -30f + 60f * i / (count - 1);
                    var shard = pool.Get();
                    shard.transform.localScale = projectileScale * 0.5f;
                    shard.SetVisualForm(WeaponForm.Default);
                    shard.Init(pool, new ProjectileSpawnSettings
                    {
                        StartPos = position,
                        Direction = Quaternion.Euler(0, 0, angle) * direction,
                        Speed = shardSpeed,
                        Lifetime = ProjectileLifetime,
                        TargetProvider = targetProvider,
                        OnHit = auxiliaryHit,
                        IgnoredTarget = target,
                        HitReactions = new HitReactionBuilder()
                            .Damage(shardDamage)
                            .Frostbite(shardDamage * shardFrostbite, stats.Status.FrostbiteChance)
                            .LightningStrike(auxiliaryLightningDamage)
                            .Build()
                    });
                }
            };
        }

    }
}
