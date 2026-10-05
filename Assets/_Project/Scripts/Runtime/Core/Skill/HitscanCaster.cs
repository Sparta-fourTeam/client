using UnityEngine;
using UnityEngine.Pool;

namespace Game.Core
{
    public class HitscanCaster : WeaponBase
    {
        private ObjectPool<HitscanEffect> pool;
        private readonly SkillObjectPool<HitscanEffect> ownedPool;
        private readonly SkillObjectPool<Projectile> ownedOrbPool;
        private readonly SkillObjectPool<ElectromagneticField> ownedFieldPool;
        private ObjectPool<Projectile> orbPool;
        private readonly ObjectPool<ElectromagneticField> fieldPool;
        private readonly Vector3 originalScale;

        public HitscanCaster(WeaponData data, GameObject prefab, Transform caster, IEnemyTargetProvider targetProvider, SkillConfig config = null) : base(data, caster, targetProvider, config)
        {
            ownedFieldPool = new SkillObjectPool<ElectromagneticField>(
                () => new GameObject("ElectromagneticField_Prototype").AddComponent<ElectromagneticField>(), 8, 120);
            fieldPool = ownedFieldPool.Pool;
            originalScale = prefab.transform.localScale;
            var secondary = prefab.GetComponent<HitscanEffect>()?.SecondaryProjectilePrefab;
            if (secondary != null)
            {
                ownedOrbPool = new SkillObjectPool<Projectile>(() => Object.Instantiate(secondary).GetComponent<Projectile>(), 6, 120);
                orbPool = ownedOrbPool.Pool;
            }
            ownedPool = new SkillObjectPool<HitscanEffect>(() => Object.Instantiate(prefab).GetComponent<HitscanEffect>(), 5, 20);
            pool = ownedPool.Pool;
        }

        public override void Dispose()
        {
            base.Dispose(); ownedPool.Dispose(); ownedOrbPool?.Dispose(); ownedFieldPool.Dispose();
        }

        protected override void OnFire()
        {
            var config = Config;
            var current = config.Stats;
            var targets = FindTargets(config.Attack.Range);
            if (targets.Count == 0) { return; }
            var effects = new HitscanCastEffects(current, data.baseStats, targetProvider, pool, orbPool, fieldPool, originalScale, config.Reactions);
            for (int i = 0; i < current.Cast.ProjectileCount; i++)
            {
                effects.Cast(targets[i % targets.Count]);
            }
        }
    }
}
