using UnityEngine;
using UnityEngine.Pool;

namespace Game.Core
{
    /// <summary>대상 위치에 영역을 깔고 지속 시간 동안 주기마다 피해와 상태이상을 건다 (냉기 지대 등).</summary>
    public sealed class AreaStrategy : IAttackStrategy
    {
        private readonly SkillObjectPool<AreaZone> ownedPool;

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
            for (int i = 0; i < stats.Cast.ProjectileCount; i++)
            {
                var target = targets[i % targets.Count];
                Pool.Get().Init(Pool, environment.Targets, target.Position, AreaSettings.From(stats.Area), hitReactions, config.Reactions);
            }

            return true;
        }
    }
}
