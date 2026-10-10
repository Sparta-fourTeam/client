using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace Game.Core
{
    /// <summary>투사체를 발사한다. 예비 시전(성벽 앞 적 감지)도 이 전략이 가진다.</summary>
    public sealed class ProjectileStrategy : IAttackStrategy
    {
        private readonly SkillObjectPool<Projectile> ownedPool;
        private readonly Game.Core.Defense.Wall wall;
        private SkillTargetSelector laneSelector;
        private readonly List<IEnemyTarget> laneBuffer = new();
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
                if (candidate == null || candidate.IsDead) { continue; }
                if (candidate.Position.y <= wall.AttackLineY + cast.ReserveDistance
                    && (candidate.Position - (Vector2)origin).sqrMagnitude <= config.Attack.Range * config.Attack.Range)
                { nearby = true; break; }
            }
            reserveConfig = config;
            reserveEnvironment = environment;
            reserveClock.Tick(deltaTime, nearby, cast.ReserveCount, cast.ReserveCooldown, cast.ReserveInterval, fireReserve);
        }

        // 굴러가는 공격(나무뿌리)은 플레이어에서 발사되지 않으므로, 사거리를 벽 앞선에서 위로 높이(y)만 재고 가로 위치(x)는 보지 않는다.
        // 그 밖의 공격은 시전 위치에서의 거리로 잰다
        private IReadOnlyList<IEnemyTarget> FindTargets(SkillConfig config, AttackEnvironment environment)
        {
            if (config.Attack.Path != ProjectilePath.RollingLane || wall == null) { return environment.FindTargets(config.Attack.Range); }
            laneSelector ??= new SkillTargetSelector(environment.Targets);
            var lane = laneSelector.SelectLane(wall.AttackLineY, config.Attack.Range);
            if (environment.Exclude == null) { return lane; }
            laneBuffer.Clear();
            foreach (var target in lane) { if (!ReferenceEquals(target, environment.Exclude)) { laneBuffer.Add(target); } }
            return laneBuffer;
        }

        public bool HasTargets(SkillConfig config, AttackEnvironment environment)
        {
            foreach (var target in FindTargets(config, environment)) { if (!target.IsDead) { return true; } }
            return false;
        }

        public bool Fire(SkillConfig config, AttackEnvironment environment)
        {
            var current = config.Stats;
            // 사방 발사는 조준하지 않으므로 대상이 없어도 쏜다. 그 밖에는 가까운 적부터 서로 다른 적에게 나눠 쏜다.
            bool radial = config.Attack.Path == ProjectilePath.Radial;
            var targets = radial ? null : FindTargets(config, environment);
            // 맞은 적을 빼고 나니 노릴 적이 없으면, 부모가 날아온 방향이 있을 때 그 방향 기준 부채꼴로 쏜다.
            bool fan = !radial && targets.Count == 0 && environment.Exclude != null && environment.Direction.sqrMagnitude > 0;
            if (!radial && !fan && targets.Count == 0) { return false; }

            var spawnRules = new ProjectileSpawnRules(current, environment.Targets, Pool, Scale, config.Reactions);
            int count = current.Cast.ProjectileCount;
            for (int i = 0; i < count; i++)
            {
                Vector2 aim = radial || fan ? default : targets[i % targets.Count].Position;
                var path = fan
                    ? ProjectileLaunchPath.Fan(environment.Origin, environment.Direction, current.Projectile.PierceCount, i, count)
                    : ProjectileLaunchPath.Calculate(config.Attack.Path, environment.Origin, aim,
                        wall != null ? wall.AttackLineY : (float?)null, config.Attack.Range, current.Projectile.Speed, current.Projectile.PierceCount, i, count);
                spawnRules.SpawnMain(path.Start, path.Direction, path.Lifetime, path.PierceCount, environment.Exclude);
            }

            return true;
        }
    }
}
