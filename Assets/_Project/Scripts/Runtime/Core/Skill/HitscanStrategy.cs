using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace Game.Core
{
    /// <summary>대상 위치에 즉시 타격 이펙트를 낸다. 전기 구체와 전자기장 풀도 이 전략이 가진다.</summary>
    public sealed class HitscanStrategy : IAttackStrategy
    {
        private readonly SkillObjectPool<HitscanEffect> ownedPool;
        private readonly Dictionary<SkillForm, GameObject> formPrefabs = new();
        private readonly Dictionary<SkillForm, SkillObjectPool<HitscanEffect>> formPools = new();
        private readonly SkillObjectPool<ElectromagneticField> ownedFieldPool;
        private readonly ObjectPool<ElectromagneticField> fieldPool;
        private readonly Vector3 originalScale;

        public ObjectPool<HitscanEffect> Pool { get; }

        public HitscanStrategy(GameObject prefab, IReadOnlyList<SkillFormPrefabEntry> formPrefabs = null)
        {
            ownedFieldPool = new SkillObjectPool<ElectromagneticField>(
                () => new GameObject("ElectromagneticField_Prototype").AddComponent<ElectromagneticField>(), 8, 120);
            fieldPool = ownedFieldPool.Pool;
            originalScale = prefab.transform.localScale;
            ownedPool = new SkillObjectPool<HitscanEffect>(() => Object.Instantiate(prefab).GetComponent<HitscanEffect>(), 5, 20);
            Pool = ownedPool.Pool;
            if (formPrefabs != null)
            {
                foreach (var entry in formPrefabs)
                {
                    if (entry != null && entry.prefab != null) { this.formPrefabs[entry.form] = entry.prefab; }
                }
            }
        }

        public void Dispose()
        {
            ownedPool.Dispose();
            foreach (var pool in formPools.Values) { pool.Dispose(); }
            formPools.Clear();
            ownedFieldPool.Dispose();
        }

        public void Tick(SkillConfig config, AttackEnvironment environment, float deltaTime) { }

        public bool Fire(SkillConfig config, AttackEnvironment environment)
        {
            var current = config.Stats;
            var targets = environment.FindTargets(config.Attack.Range);
            if (targets.Count == 0) { return false; }
            var pool = Pool;
            var scale = originalScale;
            if (formPrefabs.TryGetValue(current.Cast.Form, out var formPrefab))
            {
                if (!formPools.TryGetValue(current.Cast.Form, out var formPool))
                {
                    formPool = new SkillObjectPool<HitscanEffect>(() => Object.Instantiate(formPrefab).GetComponent<HitscanEffect>(), 5, 20);
                    formPools.Add(current.Cast.Form, formPool);
                }
                pool = formPool.Pool;
                scale = formPrefab.transform.localScale;
            }
            var effects = new HitscanCastEffects(current, config.Attack.Range, environment.Targets, pool, fieldPool, scale, config.Reactions,
                secondaryStrikes: Pool, secondaryScale: originalScale);
            for (int i = 0; i < current.Cast.ProjectileCount; i++)
            {
                effects.Cast(targets[i % targets.Count]);
            }

            return true;
        }
    }
}
