using UnityEngine;
using UnityEngine.Pool;

namespace Game.Core
{
    public class HitscanCaster : WeaponBase
    {
        private ObjectPool<HitscanEffect> pool;

        public HitscanCaster(WeaponData data, GameObject prefab, Transform caster, IEnemyTargetProvider targetProvider) : base(data, caster, targetProvider)
        {
            pool = new ObjectPool<HitscanEffect>(
                createFunc: () => Object.Instantiate(prefab).GetComponent<HitscanEffect>(),
                actionOnGet: p => p.gameObject.SetActive(true),
                actionOnRelease: p => p.gameObject.SetActive(false),
                actionOnDestroy: p => { if (p != null) { Object.Destroy(p.gameObject); } },
                collectionCheck: true,
                defaultCapacity: 5,
                maxSize: 20
            );
        }

        protected override void OnFire()
        {
            var targets = FindTargets(data.baseStats.range, stats.ProjectileCount);
            if (targets.Count == 0) { return; }

            for (int i = 0; i < stats.ProjectileCount; i++)
            {
                var target = targets[i % targets.Count];

                var effect = pool.Get();
                float explosionRadius = stats.ExplosionRadius;
                float explosionDamage = stats.ExplosionDamage;
                System.Action<Vector2> onHit = explosionRadius > 0
                    ? position => AreaDamage.Apply(targetProvider, position, explosionRadius, explosionDamage)
                    : null;
                effect.Init(pool, new Vector3(target.Position.x, target.Position.y, 0f), stats.Damage, onHit);
            }
        }
    }
}
