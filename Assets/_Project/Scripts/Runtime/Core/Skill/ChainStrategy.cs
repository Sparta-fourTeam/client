using UnityEngine;
using UnityEngine.Pool;

namespace Game.Core
{
    /// <summary>가장 가까운 적을 맞히고 가까운 다른 적으로 연쇄적으로 튕기며 공격한다 (연쇄 번개 등).
    /// 시전 수(projectileCount)만큼 서로 다른 첫 대상을 노려 연쇄를 시작한다.</summary>
    public sealed class ChainStrategy : IAttackStrategy
    {
        private readonly SkillObjectPool<ChainBolt> ownedPool;

        public ObjectPool<ChainBolt> Pool { get; }

        public ChainStrategy(GameObject prefab)
        {
            ownedPool = new SkillObjectPool<ChainBolt>(() => Object.Instantiate(prefab).GetComponent<ChainBolt>(), 4, 20);
            Pool = ownedPool.Pool;
        }

        public void Dispose() => ownedPool.Dispose();

        public void Tick(SkillConfig config, AttackEnvironment environment, float deltaTime) { }

        public bool Fire(SkillConfig config, AttackEnvironment environment)
        {
            var stats = config.Stats;
            var targets = environment.FindTargets(config.Attack.Range);
            if (targets.Count == 0) { return false; }
            var hitReactions = ReactionCompiler.ForChain(stats, environment.Targets);
            var settings = ChainSettings.From(stats.Chain);
            for (int i = 0; i < stats.Cast.ProjectileCount; i++)
            {
                var first = targets[i % targets.Count];
                Pool.Get().Init(Pool, environment.Targets, environment.Origin, first, settings, hitReactions, config.Reactions);
            }

            return true;
        }
    }
}
