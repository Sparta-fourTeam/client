using UnityEngine;
using UnityEngine.Pool;

namespace Game.Core
{
    /// <summary>Spawn rules for primary, triangle branches and auxiliary projectiles.</summary>
    public sealed class ProjectileSpawnRules
    {
        private readonly IWeaponStats stats;
        private readonly WeaponData data;
        private readonly IEnemyTargetProvider targetProvider;
        private readonly ObjectPool<Projectile> pool;
        private readonly Vector3 projectileScale;
        private const float ProjectileLifetime = 3f;

        public ProjectileSpawnRules(IWeaponStats stats, WeaponData data, IEnemyTargetProvider targetProvider,
            ObjectPool<Projectile> pool, Vector3 projectileScale)
        {
            this.stats = stats;
            this.data = data;
            this.targetProvider = targetProvider;
            this.pool = pool;
            this.projectileScale = projectileScale;
        }

        public void SpawnMain(Vector3 start, Vector3 direction, float lifetime, int pierce)
        {
            Projectile projectile = pool.Get();
            projectile.transform.localScale = stats.Form == WeaponForm.Enbakutsu
                ? Vector3.Scale(projectileScale, new Vector3(1.5f, 0.65f, 1)) : projectileScale;
            projectile.transform.localScale *= stats.ProjectileSizeMultiplier;
            projectile.SetVisualForm(stats.Form);
            projectile.Init(pool, new ProjectileSpawnSettings
            {
                StartPos = start,
                Direction = direction,
                Damage = stats.Damage,
                Speed = stats.ProjectileSpeed,
                Lifetime = lifetime,
                TargetProvider = targetProvider,
                PierceCount = pierce,
                FreezeDuration = stats.FreezeDuration,
                OnHit = CreateHitCallback(),
                KnockbackDistance = stats.KnockbackDistance,
                FrostbiteRatio = stats.FrostbiteRatio,
                ParalysisDuration = stats.ParalysisDuration,
                LightningDamage = stats.Damage * stats.LightningStrikeRatio,
                ParalysisChance = data.baseStats.paralysisChance,
                BurnDuration = stats.BurnDuration,
                BurnDamage = stats.Damage * stats.BurnRatio,
                BurnMaxHpRatio = stats.BurnMaxHpRatio,
                BurnOnDeath = CreateBurnDeathCallback(),
                FreezeChance = data.baseStats.freezeChance,
                FrostbiteChance = data.baseStats.frostbiteChance,
                BurnChance = data.baseStats.burnChance,
                CollisionRadius = .3f * stats.ProjectileSizeMultiplier,
                StunDuration = stats.StunDuration,
                StunChance = data.baseStats.stunChance,
                SlowDuration = stats.SlowDuration,
                SlowRatio = stats.SlowRatio,
                VulnerabilityRatio = stats.VulnerabilityRatio,
                VulnerabilityDuration = stats.VulnerabilityDuration
            });
        }
        private System.Action<Vector2> CreateBurnDeathCallback()
        {
            if (!stats.BurnDeathExplosion)
            {
                return null;
            }

            float radius = stats.ExplosionRadius;
            float damage = stats.ExplosionDamage;
            return position => AreaDamage.Apply(targetProvider, position, radius, damage);
        }

        private System.Action<Vector2, Vector3, IEnemyTarget> CreateHitCallback()
        {
            var split = stats.Form == WeaponForm.TriangleIce ? CreateTriangleCallback() : CreateSplitCallback();
            float radius = stats.ExplosionRadius;
            float damage = stats.ExplosionDamage;
            if (radius <= 0)
            {
                return split;
            }

            return (position, direction, target) =>
            {
                AreaDamage.Apply(targetProvider, position, radius, damage);
                split?.Invoke(position, direction, target);
            };
        }

        private System.Action<Vector2, Vector3, IEnemyTarget> CreateTriangleCallback()
        {
            float damage = stats.Damage * 0.5f;
            float speed = stats.ProjectileSpeed;
            int pierce = stats.PierceCount;
            float freeze = stats.FreezeDuration;
            float knockback = stats.KnockbackDistance;
            float frostbite = stats.FrostbiteRatio;
            var normalSplit = CreateSplitCallbackForDamage(damage);
            float freezeChance = data.baseStats.freezeChance;
            float frostbiteChance = data.baseStats.frostbiteChance;
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
                        Damage = damage,
                        Speed = speed,
                        Lifetime = ProjectileLifetime,
                        TargetProvider = targetProvider,
                        PierceCount = pierce,
                        FreezeDuration = freeze,
                        OnHit = normalSplit,
                        IgnoredTarget = source,
                        KnockbackDistance = knockback,
                        FrostbiteRatio = frostbite,
                        FreezeChance = freezeChance,
                        FrostbiteChance = frostbiteChance
                    });
                }
            };
        }

        private System.Action<Vector2, Vector3, IEnemyTarget> CreateSplitCallback() => CreateSplitCallbackForDamage(stats.Damage);

        private System.Action<Vector2, Vector3, IEnemyTarget> CreateSplitCallbackForDamage(float sourceDamage)
        {
            int count = stats.SplitCount;
            if (count <= 0)
            {
                return null;
            }

            float shardDamage = sourceDamage * 0.5f * stats.ShardDamageMultiplier;
            float shardSpeed = stats.ProjectileSpeed;
            float shardFrostbite = stats.ShardFrostbiteRatio;
            float auxiliaryLightningDamage = shardDamage * stats.AuxiliaryLightningRatio;
            float auxiliaryExplosionRadius = stats.AuxiliaryExplosions ? stats.ExplosionRadius : 0;
            float auxiliaryExplosionDamage = stats.ExplosionDamage * 0.5f * stats.ShardDamageMultiplier;
            System.Action<Vector2, Vector3, IEnemyTarget> auxiliaryHit = auxiliaryExplosionRadius > 0
                ? (position, direction, target) => AreaDamage.Apply(targetProvider, position, auxiliaryExplosionRadius, auxiliaryExplosionDamage)
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
                        Damage = shardDamage,
                        Speed = shardSpeed,
                        Lifetime = ProjectileLifetime,
                        TargetProvider = targetProvider,
                        OnHit = auxiliaryHit,
                        IgnoredTarget = target,
                        FrostbiteRatio = shardFrostbite,
                        LightningDamage = auxiliaryLightningDamage,
                        FrostbiteChance = data.baseStats.frostbiteChance
                    });
                }
            };
        }

        public static System.Action<Vector2> CreateOrbCallback(IWeaponStats stats, WeaponData data, IEnemyTargetProvider targetProvider, ObjectPool<Projectile> orbPool, IEnemyTarget sourceTarget)
        {
            int count = stats.SplitCount;
            if (orbPool == null || count <= 0) { return null; }
            float damage = stats.Damage * 0.5f * stats.ShardDamageMultiplier;
            float paralysis = stats.AuxiliaryParalysisDuration;
            float chance = data.baseStats.paralysisChance;
            return position =>
            {
                for (int i = 0; i < count; i++)
                {
                    float angle = i * Mathf.PI * 2 / count;
                    var direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
                    orbPool.Get().Init(orbPool, new ProjectileSpawnSettings
                    {
                        StartPos = position,
                        Direction = direction,
                        Damage = damage,
                        Speed = 10,
                        Lifetime = 3,
                        TargetProvider = targetProvider,
                        IgnoredTarget = sourceTarget,
                        ParalysisDuration = paralysis,
                        ParalysisChance = chance
                    });
                }
            };
        }
    }
}
