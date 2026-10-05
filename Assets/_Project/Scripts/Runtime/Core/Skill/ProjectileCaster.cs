using UnityEngine;
using UnityEngine.Pool;

namespace Game.Core
{
    public class ProjectileCaster : WeaponBase
    {
        private ObjectPool<Projectile> pool;
        private readonly Game.Core.Defense.Wall wall;
        private readonly ReactiveCastClock reserveClock = new();
        private readonly Vector3 projectileScale;
        private const float ProjectileLifetime = 3f;
        private const float RollingWallFrontOffset = .2f;

        public ProjectileCaster(WeaponData data, GameObject prefab, Transform caster, IEnemyTargetProvider targetProvider, Game.Core.Defense.Wall wall = null) : base(data, caster, targetProvider)
        {
            this.wall = wall;
            projectileScale = prefab.transform.localScale;
            pool = new ObjectPool<Projectile>(
                createFunc: () => Object.Instantiate(prefab).GetComponent<Projectile>(),
                actionOnGet: p => p.gameObject.SetActive(true),
                actionOnRelease: p => p.gameObject.SetActive(false),
                actionOnDestroy: p => { if (p != null) { Object.Destroy(p.gameObject); } },
                collectionCheck: true,
                defaultCapacity: 10,
                maxSize: 10
            );
        }

        protected override void OnTickExtra(float deltaTime)
        {
            if (stats.ReserveCastCount <= 0 || wall == null || wall.IsDestroyed) { return; }
            bool nearby = false;
            var candidates = new System.Collections.Generic.List<IEnemyTarget>();
            targetProvider.GetNearest(new Vector2(caster.position.x, wall.AttackLineY), int.MaxValue, candidates);
            foreach (var candidate in candidates)
            {
                if (candidate == null || candidate is EnemyModel model && model.IsDead) { continue; }
                if (candidate.Position.y <= wall.AttackLineY + data.baseStats.reserveDistance
                    && (candidate.Position - (Vector2)caster.position).sqrMagnitude <= data.baseStats.range * data.baseStats.range)
                { nearby = true; break; }
            }
            reserveClock.Tick(deltaTime, nearby, stats.ReserveCastCount, data.baseStats.reserveCooldown, data.baseStats.reserveInterval, OnFire);
        }

        protected override void OnFire()
        {
            var targets = FindTargets(data.baseStats.range, stats.ProjectileCount);
            if (targets.Count == 0) { return; }

            for (int i = 0; i < stats.ProjectileCount; i++)
            {
                var target = targets[i % targets.Count];
                bool rolling = data.projectilePath == ProjectilePath.RollingLane;
                float startY = wall != null ? wall.AttackLineY + RollingWallFrontOffset : caster.position.y;
                var start = rolling ? new Vector3(target.Position.x, startY, caster.position.z) : caster.position;
                var dir2D = rolling ? Vector2.up : (target.Position - (Vector2)caster.position).normalized;
                var direction = new Vector3(dir2D.x, dir2D.y, 0f);
                float lifetime = rolling ? data.baseStats.range / Mathf.Max(.01f, stats.ProjectileSpeed) + .1f : ProjectileLifetime;
                int pierce = rolling ? int.MaxValue - 1 : stats.PierceCount;

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

    }
}
