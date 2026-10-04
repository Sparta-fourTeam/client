using UnityEngine;
using UnityEngine.Pool;

namespace Game.Core
{
    public class ProjectileCaster : WeaponBase
    {
        private ObjectPool<Projectile> pool;
        private const float ProjectileLifetime = 3f;

        public ProjectileCaster(WeaponData data, GameObject prefab, Transform caster, IEnemyTargetProvider targetProvider) : base(data, caster, targetProvider)
        {
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
                projectile.Init(pool, caster.position, direction, stats.Damage, stats.ProjectileSpeed, ProjectileLifetime, targetProvider, stats.PierceCount, stats.FreezeDuration);
            }
        }
    }
}
