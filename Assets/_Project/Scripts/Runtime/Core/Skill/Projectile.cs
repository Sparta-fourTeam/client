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

        public void Init(IObjectPool<Projectile> pool, Vector3 startPos, Vector3 direction, float damage, float speed)
        {
            this.pool = pool;
            transform.position = startPos;
            this.direction = direction.normalized;
            this.damage = damage;
            this.speed = speed;
        }

        private void Update()
        {
            transform.position += direction * speed * Time.deltaTime;
        }
    }
}
