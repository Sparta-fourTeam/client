using UnityEngine;
using UnityEngine.Pool;

namespace Game.Core
{
    public class Projectile : MonoBehaviour
    {
        private IObjectPool<Projectile> pool;
        private Vector3 direction;
        private float damage;
        private float speed;
        private float lifetime;

        public void Init(IObjectPool<Projectile> pool, Vector3 startPos, Vector3 direction, float damage, float speed, float lifetime)
        {
            this.pool = pool;
            transform.position = startPos;
            this.direction = direction.normalized;
            this.damage = damage;
            this.speed = speed;
            this.lifetime = lifetime;
        }

        private void Update()
        {
            transform.position += direction * speed * Time.deltaTime;

            lifetime -= Time.deltaTime;
            if (lifetime <= 0)
            {
                pool.Release(this);
            }
        }
    }
}
