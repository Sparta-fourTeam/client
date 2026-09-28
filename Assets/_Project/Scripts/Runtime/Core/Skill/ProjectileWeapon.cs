using UnityEngine;
using UnityEngine.Pool;

namespace Game.Core
{
    public class ProjectileWeapon : WeaponBase
    {
        private ObjectPool<Projectile> pool;

        public ProjectileWeapon(WeaponData data, GameObject prefab, Transform owner) : base(data, owner)
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
            projectile.Init(pool, owner.position, Vector3.up, currentDamage, data.baseStats.speed);
        }
    }
}
