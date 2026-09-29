using UnityEngine;
using UnityEngine.Pool;

namespace Game.Core
{
    public class ProjectileCaster : WeaponBase
    {
        private ObjectPool<Projectile> pool;
        private const float ProjectileLifetime = 3f;

        public ProjectileCaster(WeaponData data, GameObject prefab, Transform caster) : base(data, caster)
        {
            pool = new ObjectPool<Projectile>(
                createFunc: () => Object.Instantiate(prefab).GetComponent<Projectile>(),
                actionOnGet: p => p.gameObject.SetActive(true),
                actionOnRelease: p => p.gameObject.SetActive(false),
                actionOnDestroy: p => Object.Destroy(p.gameObject),
                collectionCheck: true,
                defaultCapacity: 10,
                maxSize: 10
            );
        }

        protected override void OnFire()
        {
            Debug.Log("OnFire()");
            Projectile projectile = pool.Get();
            projectile.Init(pool, caster.position, Vector3.up, currentDamage, data.baseStats.speed, ProjectileLifetime);
        }
    }
}
