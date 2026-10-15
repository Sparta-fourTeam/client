using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace Game.Core
{
    /// <summary>대상 위치에 영역을 깔고 지속 시간 동안 주기마다 피해와 상태이상을 건다 (냉기 지대 등).</summary>
    public sealed partial class AreaStrategy : IAttackStrategy
    {
        private readonly SkillObjectPool<AreaZone> ownedPool;
        private readonly HashSet<IEnemyTarget> burstTargets = new();

        public ObjectPool<AreaZone> Pool { get; }

        public AreaStrategy(GameObject prefab)
        {
            ownedPool = new SkillObjectPool<AreaZone>(() => Object.Instantiate(prefab).GetComponent<AreaZone>(), 5, 20);
            Pool = ownedPool.Pool;
        }

        public void Dispose() => ownedPool.Dispose();

        public void Tick(SkillConfig config, AttackEnvironment environment, float deltaTime) { }

        public bool Fire(SkillConfig config, AttackEnvironment environment)
        {
            var stats = config.Stats;
            var targets = environment.FindTargets(config.Attack.Range);
            if (targets.Count == 0) { return false; }
            var hitReactions = ReactionCompiler.ForProjectile(stats, null, environment.Targets);
            // 연발의 각 시전은 이번 주기에 아직 노리지 않은 적을 먼저 노린다. 모두 노렸으면 처음부터 다시 돈다
            if (environment.CastIndex == 0) { burstTargets.Clear(); }
            for (int i = 0; i < stats.Cast.ProjectileCount; i++)
            {
                var target = NextTarget(targets, environment.CastIndex * stats.Cast.ProjectileCount + i);
                burstTargets.Add(target);
                Pool.Get().Init(Pool, environment.Targets, target.Position, AreaSettings.From(stats.Area), hitReactions, config.Reactions);
            }

            return true;
        }

        private IEnemyTarget NextTarget(IReadOnlyList<IEnemyTarget> targets, int fallbackIndex)
        {
            foreach (var target in targets)
            {
                if (!target.IsDead && !burstTargets.Contains(target)) { return target; }
            }
            return targets[fallbackIndex % targets.Count];
        }
    }
}
