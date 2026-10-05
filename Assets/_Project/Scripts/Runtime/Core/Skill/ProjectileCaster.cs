using UnityEngine;
using UnityEngine.Pool;

namespace Game.Core
{
    public class ProjectileCaster : WeaponBase
    {
        private ObjectPool<Projectile> pool;
        private readonly SkillObjectPool<Projectile> ownedPool;
        private readonly Game.Core.Defense.Wall wall;
        private readonly ReactiveCastClock reserveClock = new();
        private readonly Vector3 projectileScale;

        public ProjectileCaster(WeaponData data, GameObject prefab, Transform caster, IEnemyTargetProvider targetProvider, Game.Core.Defense.Wall wall = null, SkillConfig config = null) : base(data, caster, targetProvider, config)
        {
            this.wall = wall;
            projectileScale = prefab.transform.localScale;
            ownedPool = new SkillObjectPool<Projectile>(() => Object.Instantiate(prefab).GetComponent<Projectile>(), 10, 10);
            pool = ownedPool.Pool;
        }

        public override void Dispose() { base.Dispose(); ownedPool.Dispose(); }

        protected override void OnTickExtra(float deltaTime)
        {
            var current = Stats;
            if (current.Cast.ReserveCount <= 0 || wall == null || wall.IsDestroyed) { return; }
            bool nearby = false;
            var candidates = new System.Collections.Generic.List<IEnemyTarget>();
            targetProvider.GetNearest(new Vector2(caster.position.x, wall.AttackLineY), int.MaxValue, candidates);
            foreach (var candidate in candidates)
            {
                if (candidate == null || candidate is EnemyModel model && model.IsDead) { continue; }
                if (candidate.Position.y <= wall.AttackLineY + current.Cast.ReserveDistance
                    && (candidate.Position - (Vector2)caster.position).sqrMagnitude <= Config.Attack.Range * Config.Attack.Range)
                { nearby = true; break; }
            }
            reserveClock.Tick(deltaTime, nearby, current.Cast.ReserveCount, current.Cast.ReserveCooldown, current.Cast.ReserveInterval, OnFire);
        }

        protected override void OnFire()
        {
            var config = Config;
            var current = config.Stats;
            var targets = FindTargets(config.Attack.Range);
            if (targets.Count == 0) { return; }

            var spawnRules = new ProjectileSpawnRules(current, targetProvider, pool, projectileScale, config.Reactions);
            for (int i = 0; i < current.Cast.ProjectileCount; i++)
            {
                var target = targets[i % targets.Count];
                var path = ProjectileLaunchPath.Calculate(config.Attack.Path, caster.position, target.Position,
                    wall != null ? wall.AttackLineY : (float?)null, config.Attack.Range, current.Projectile.Speed, current.Projectile.PierceCount);
                spawnRules.SpawnMain(path.Start, path.Direction, path.Lifetime, path.PierceCount);
            }
        }
    }
}
