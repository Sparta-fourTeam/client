using UnityEngine;
using UnityEngine.Pool;

namespace Game.Core
{
    public class HitscanEffect : MonoBehaviour
    {
        private IObjectPool<HitscanEffect> pool;
        private float lifetime;
        [SerializeField] private LayerMask targetMask;
        private float damage;
        private float radius;




        public void Init(IObjectPool<HitscanEffect> pool, Vector3 position, float damage, float lifetime)
        {

            this.pool = pool;
            transform.position = position;
            this.damage = damage;
            this.lifetime = lifetime;
            Invoke(nameof(Hit), 0.1f);
        }

        private void Update()
        {
            lifetime -= Time.deltaTime;
            if (lifetime <= 0)
            {
                pool.Release(this);
            }
        }

        private void Hit()
        {
            if (!gameObject.activeSelf)
            {
                return;
            }

            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, 3f, targetMask);

            foreach (Collider2D hit in hits)
            {
                if (hit.TryGetComponent(out Enemy enemy))
                {
                    enemy.TakeDamage((int)damage);
                }
            }
        }
    }
}
