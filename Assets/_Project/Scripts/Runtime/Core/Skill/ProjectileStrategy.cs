using System;
using UnityEngine;
using UnityEngine.Pool;

namespace Game.Core
{
    /// <summary>투사체를 발사한다. 예비 시전(성벽 앞 적 감지)도 이 전략이 가진다.</summary>
    public sealed class ProjectileStrategy : IAttackStrategy
    {
        private readonly SkillObjectPool<Projectile> ownedPool;
        private readonly Game.Core.Defense.Wall wall;
        private readonly ReactiveCastClock reserveClock = new();
        private readonly Action fireReserve;
        private SkillConfig reserveConfig;
        private AttackEnvironment reserveEnvironment;

        public ObjectPool<Projectile> Pool { get; }
        public Vector3 Scale { get; }

        public ProjectileStrategy(GameObject prefab, Game.Core.Defense.Wall wall = null)
        {
            this.wall = wall;
            Scale = prefab.transform.localScale;
            ownedPool = new SkillObjectPool<Projectile>(() => UnityEngine.Object.Instantiate(prefab).GetComponent<Projectile>(), 10, 10);
            Pool = ownedPool.Pool;
            fireReserve = () => Fire(reserveConfig, reserveEnvironment);
        }

        public void Dispose() => ownedPool.Dispose();

        public void Tick(SkillConfig config, AttackEnvironment environment, float deltaTime)
        {
            var cast = config.Stats.Cast;
            if (cast.ReserveCount <= 0 || wall == null || wall.IsDestroyed) { return; }
            Vector3 origin = environment.Origin;
            bool nearby = false;
            var candidates = new System.Collections.Generic.List<IEnemyTarget>();
            environment.Targets.GetNearest(new Vector2(origin.x, wall.AttackLineY), int.MaxValue, candidates);
            foreach (var candidate in candidates)
            {
                if (candidate == null || candidate is EnemyModel model && model.IsDead) { continue; }
                if (candidate.Position.y <= wall.AttackLineY + cast.ReserveDistance
                    && (candidate.Position - (Vector2)origin).sqrMagnitude <= config.Attack.Range * config.Attack.Range)
                { nearby = true; break; }
            }
            reserveConfig = config;
            reserveEnvironment = environment;
            reserveClock.Tick(deltaTime, nearby, cast.ReserveCount, cast.ReserveCooldown, cast.ReserveInterval, fireReserve);
        }

        public void Fire(SkillConfig config, AttackEnvironment environment)
        {
            var current = config.Stats;
            var targets = environment.FindTargets(config.Attack.Range);
            if (targets.Count == 0) { return; }

            var spawnRules = new ProjectileSpawnRules(current, environment.Targets, Pool, Scale, config.Reactions);
            for (int i = 0; i < current.Cast.ProjectileCount; i++)
            {
                var target = targets[i % targets.Count];
                var path = ProjectileLaunchPath.Calculate(config.Attack.Path, environment.Origin, target.Position,
                    wall != null ? wall.AttackLineY : (float?)null, config.Attack.Range, current.Projectile.Speed, current.Projectile.PierceCount);
                spawnRules.SpawnMain(path.Start, path.Direction, path.Lifetime, path.PierceCount, environment.Exclude);
            }
        }
    }
}
