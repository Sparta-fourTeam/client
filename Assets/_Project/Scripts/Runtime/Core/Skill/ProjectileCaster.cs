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

            var spawnRules = new ProjectileSpawnRules(stats, data, targetProvider, pool, projectileScale);
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

                spawnRules.SpawnMain(start, direction, lifetime, pierce);
            }
        }
    }
}
