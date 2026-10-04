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
                projectile.Init(pool, caster.position, direction, stats.Damage, stats.ProjectileSpeed, ProjectileLifetime, targetProvider, stats.PierceCount, stats.FreezeDuration, CreateSplitCallback(), knockbackDistance: stats.KnockbackDistance, frostbiteRatio: stats.FrostbiteRatio);
            }
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
            return (position, direction, target) =>
            {
                for (int i = 0; i < count; i++)
                {
                    float angle = count == 1 ? 0 : -30f + 60f * i / (count - 1);
                    var shard = pool.Get();
                    shard.transform.localScale = projectileScale * 0.5f;
                    shard.Init(pool, position, Quaternion.Euler(0, 0, angle) * direction,
                        shardDamage, shardSpeed, ProjectileLifetime, targetProvider, ignoredTarget: target, frostbiteRatio: shardFrostbite);
                }
            };
        }

    }
}
