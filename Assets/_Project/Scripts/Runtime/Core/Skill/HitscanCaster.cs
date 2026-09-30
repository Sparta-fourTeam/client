using UnityEngine;
using UnityEngine.Pool;

namespace Game.Core
{
    public class HitscanCaster : WeaponBase
    {
        private ObjectPool<HitscanEffect> pool;
        private const float EffectLifetime = 0.3f;



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
            for (int i = 0; i < stats.HitCount; i++)
            {
                var target = FindTarget(data.baseStats.range);
                if (target == null)
                {
                    Debug.Log("OnFire() [Hitscan] 타겟 없음");
                    continue;
                }

                var effect = pool.Get();
                effect.Init(pool, new Vector3(target.Position.x, target.Position.y, 0f), stats.Damage, EffectLifetime);

            }
        }
    }
}
