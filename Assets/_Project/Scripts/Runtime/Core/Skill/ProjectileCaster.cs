using UnityEngine;
using UnityEngine.Pool;

namespace Game.Core
{
    public class ProjectileCaster : WeaponBase
    {
        private ObjectPool<Projectile> pool;
        private readonly Vector3 projectileScale;
        private const float ProjectileLifetime = 3f;

        public ProjectileCaster(WeaponData data, GameObject prefab, Transform caster, IEnemyTargetProvider targetProvider) : base(data, caster, targetProvider)
        {
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

        protected override void OnFire()
        {
            var targets = FindTargets(data.baseStats.range, stats.ProjectileCount);
            if (targets.Count == 0) { return; }

            for (int i = 0; i < stats.ProjectileCount; i++)
            {
                var target = targets[i % targets.Count];
                var dir2D = (target.Position - (Vector2)caster.position).normalized;
                var direction = new Vector3(dir2D.x, dir2D.y, 0f);

                Projectile projectile = pool.Get();
                projectile.transform.localScale = projectileScale;
                projectile.Init(pool, caster.position, direction, stats.Damage, stats.ProjectileSpeed, ProjectileLifetime, targetProvider, stats.PierceCount, stats.FreezeDuration, CreateHitCallback(), knockbackDistance: stats.KnockbackDistance, frostbiteRatio: stats.FrostbiteRatio, paralysisDuration: stats.ParalysisDuration, lightningDamage: stats.Damage * stats.LightningStrikeRatio, paralysisChance: data.baseStats.paralysisChance, burnDuration: stats.BurnDuration, burnDamage: stats.Damage * stats.BurnRatio, burnMaxHpRatio: stats.BurnMaxHpRatio, burnOnDeath: CreateBurnDeathCallback(), freezeChance: data.baseStats.freezeChance, frostbiteChance: data.baseStats.frostbiteChance, burnChance: data.baseStats.burnChance);
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
            var split = CreateSplitCallback();
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

        private System.Action<Vector2, Vector3, IEnemyTarget> CreateSplitCallback()
        {
            int count = stats.SplitCount;
            if (count <= 0)
            {
                return null;
            }

            float shardDamage = stats.Damage * 0.5f * stats.ShardDamageMultiplier;
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
                    shard.Init(pool, position, Quaternion.Euler(0, 0, angle) * direction,
                        shardDamage, shardSpeed, ProjectileLifetime, targetProvider, onHit: auxiliaryHit, ignoredTarget: target, frostbiteRatio: shardFrostbite, lightningDamage: auxiliaryLightningDamage, frostbiteChance: data.baseStats.frostbiteChance);
                }
            };
        }

    }
}
