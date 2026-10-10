using UnityEngine;
using UnityEngine.Pool;

namespace Game.Core
{
    /// <summary>시전 위치에서 대상 방향으로 광선을 쏘고, 지속 시간 동안 광선에 닿는 모든 적에게 정해진 횟수만큼 반응을 건다 (에너지 빔 등).
    /// 광선은 겨눈 적이 살아 있는 동안 그 적을 따라간다. 선 모양을 지원하는 AreaZone이 진행한다.</summary>
    public sealed class BeamStrategy : IAttackStrategy
    {
        private readonly SkillObjectPool<AreaZone> ownedPool;

        public ObjectPool<AreaZone> Pool { get; }

        public BeamStrategy(GameObject prefab)
        {
            ownedPool = new SkillObjectPool<AreaZone>(() => Object.Instantiate(prefab).GetComponent<AreaZone>(), 4, 20);
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
            var settings = AreaSettings.From(stats.Beam);
            for (int i = 0; i < stats.Cast.ProjectileCount; i++)
            {
                var target = targets[i % targets.Count];
                Pool.Get().Init(Pool, environment.Targets, environment.Origin, settings, hitReactions, config.Reactions, aimTarget: target);
            }

            return true;
        }
    }
}
