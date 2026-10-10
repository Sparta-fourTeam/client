using UnityEngine;
using UnityEngine.Pool;

namespace Game.Core
{
    /// <summary>대상 위치에 즉시 타격 이펙트를 낸다. 전기 구체와 전자기장 풀도 이 전략이 가진다.</summary>
    public sealed class HitscanStrategy : IAttackStrategy
    {
        private readonly SkillObjectPool<HitscanEffect> ownedPool;
        private readonly SkillObjectPool<ElectromagneticField> ownedFieldPool;
        private readonly ObjectPool<ElectromagneticField> fieldPool;
        private readonly Vector3 originalScale;

        public ObjectPool<HitscanEffect> Pool { get; }

        public HitscanStrategy(GameObject prefab)
        {
            ownedFieldPool = new SkillObjectPool<ElectromagneticField>(
                () => new GameObject("ElectromagneticField_Prototype").AddComponent<ElectromagneticField>(), 8, 120);
            fieldPool = ownedFieldPool.Pool;
            originalScale = prefab.transform.localScale;
            ownedPool = new SkillObjectPool<HitscanEffect>(() => Object.Instantiate(prefab).GetComponent<HitscanEffect>(), 5, 20);
            Pool = ownedPool.Pool;
        }

        public void Dispose()
        {
            ownedPool.Dispose();
            ownedFieldPool.Dispose();
        }

        public void Tick(SkillConfig config, AttackEnvironment environment, float deltaTime) { }

        public bool Fire(SkillConfig config, AttackEnvironment environment)
        {
            var current = config.Stats;
            var targets = environment.FindTargets(config.Attack.Range);
            if (targets.Count == 0) { return false; }
            var effects = new HitscanCastEffects(current, config.Attack.Range, environment.Targets, Pool, fieldPool, originalScale, config.Reactions);
            for (int i = 0; i < current.Cast.ProjectileCount; i++)
            {
                effects.Cast(targets[i % targets.Count]);
            }

            return true;
        }
    }
}
