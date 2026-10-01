using UnityEngine;
using UnityEngine.Pool;

namespace Game.Core
{
    public class HitscanEffect : MonoBehaviour
    {
        private IObjectPool<HitscanEffect> pool;
        [SerializeField] private LayerMask targetMask;
        private float damage;
        [SerializeField] private float radius;




        public void Init(IObjectPool<HitscanEffect> pool, Vector3 position, float damage)
        {

            this.pool = pool;
            transform.position = position;
            this.damage = damage;
        }

        private void Hit()
        {
            if (!gameObject.activeSelf)
            {
                return;
            }

            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, radius, targetMask);

            foreach (Collider2D hit in hits)
            {
                if (hit.TryGetComponent(out Enemy enemy))
                {
                    enemy.TakeDamage((int)damage);
                }
            }
        }

        private void Release()
        {
            pool.Release(this);
        }
    }
}
