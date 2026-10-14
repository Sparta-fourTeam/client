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
        private readonly Dictionary<SkillForm, GameObject> formPrefabs = new();
        private readonly Dictionary<SkillForm, SkillObjectPool<Projectile>> formPools = new();
        private readonly Game.Core.Defense.Wall wall;
        private const float ReserveGrace = .15f;
        private SkillTargetSelector laneSelector;
        private readonly List<IEnemyTarget> laneBuffer = new();
        private readonly ReactiveCastClock reserveClock = new();
        private readonly Action fireReserve;
        private SkillConfig reserveConfig;
        private AttackEnvironment reserveEnvironment;

        public ObjectPool<Projectile> Pool { get; }
        public Vector3 Scale { get; }

        public ProjectileStrategy(GameObject prefab, Game.Core.Defense.Wall wall = null,
            IReadOnlyList<SkillFormPrefabEntry> formPrefabs = null)
        {
            this.wall = wall;
            Scale = prefab.transform.localScale;
            ownedPool = new SkillObjectPool<Projectile>(() => UnityEngine.Object.Instantiate(prefab).GetComponent<Projectile>(), 10, 10);
            Pool = ownedPool.Pool;
            if (formPrefabs != null)
            {
                foreach (var entry in formPrefabs)
                {
                    if (entry != null && entry.prefab != null) { this.formPrefabs[entry.form] = entry.prefab; }
                }
            }
            fireReserve = () => Fire(reserveConfig, reserveEnvironment);
        }

        public void Dispose()
        {
            ownedPool.Dispose();
            foreach (var pool in formPools.Values) { pool.Dispose(); }
            formPools.Clear();
        }

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
                // 굴러가는 공격은 시전 판정과 같게 가로 위치(x)와 상관없이 벽 앞선에서의 높이(y)로 사정거리를 잰다
                bool inRange = config.Attack.Path == ProjectilePath.Cloud
                    ? candidate.Position.y - wall.AttackLineY <= config.Attack.Range
                    : (candidate.Position - (Vector2)origin).sqrMagnitude <= config.Attack.Range * config.Attack.Range;
                if (candidate.Position.y <= wall.AttackLineY + cast.ReserveDistance && inRange)
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
            if (config.Attack.Path != ProjectilePath.Cloud || wall == null) { return environment.FindTargets(config.Attack.Range); }
            laneSelector ??= new SkillTargetSelector(environment.Targets, environment.Reservations);
            var lane = laneSelector.SelectLane(wall.AttackLineY, config.Attack.Range);
            if (environment.Exclude == null) { return lane; }
            laneBuffer.Clear();
            foreach (var target in lane) { if (!ReferenceEquals(target, environment.Exclude)) { laneBuffer.Add(target); } }
            return laneBuffer;
        }

        // 곧 도착할 투사체의 피해를 장부에 적어, 다른 스킬이 이 적이 죽을 거라 보고 다른 적을 노리게 한다
        private static TargetReservations.Reservation ReserveHit(AttackEnvironment environment, IEnemyTarget target, SkillStats stats, float lifetime)
        {
            float speed = stats.Projectile.Speed;
            float travel = speed > 0 ? ((Vector2)target.Position - (Vector2)environment.Origin).magnitude / speed + ReserveGrace : lifetime;
            return environment.Reservations?.Reserve(target, stats.Cast.Damage, Mathf.Min(lifetime, travel));
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

            var pool = Pool;
            var scale = Scale;
            if (formPrefabs.TryGetValue(current.Cast.Form, out var formPrefab))
            {
                if (!formPools.TryGetValue(current.Cast.Form, out var formPool))
                {
                    formPool = new SkillObjectPool<Projectile>(() => UnityEngine.Object.Instantiate(formPrefab).GetComponent<Projectile>(), 10, 10);
                    formPools.Add(current.Cast.Form, formPool);
                }
                pool = formPool.Pool;
                scale = formPrefab.transform.localScale;
            }
            var spawnRules = new ProjectileSpawnRules(current, environment.Targets, pool, scale, config.Reactions);
            int count = current.Cast.ProjectileCount;
            for (int i = 0; i < count; i++)
            {
                Vector2 aim = radial || fan ? default : targets[i % targets.Count].Position;
                var path = fan
                    ? ProjectileLaunchPath.Fan(environment.Origin, environment.Direction, current.Projectile.PierceCount, i, count)
                    : ProjectileLaunchPath.Calculate(config.Attack.Path, environment.Origin, aim,
                        wall != null ? wall.AttackLineY : (float?)null,
                        config.Attack.Path == ProjectilePath.Cloud ? ProjectileLaunchPath.RollingTravelDistance : config.Attack.Range, current.Projectile.Speed, current.Projectile.PierceCount, i, count);
                var reservation = radial || fan ? null : ReserveHit(environment, targets[i % targets.Count], current, path.Lifetime);
                try { spawnRules.SpawnMain(path.Start, path.Direction, path.Lifetime, path.PierceCount, environment.Exclude, reservation); }
                catch { reservation?.Dispose(); throw; }
            }

            return true;
        }
    }
}
