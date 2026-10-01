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

        // 애니메이션 이벤트(Hit, Release) 대신 시간으로 진행하는 이펙트(파티클 프리팹)용. 0보다 작으면 쓰지 않는다 (기존 프리팹은 그대로)
        [SerializeField] private float hitDelay = -1f;
        [SerializeField] private float releaseDelay = -1f;

        private float elapsed;
        private bool hitDone;
        private bool released;




        public void Init(IObjectPool<HitscanEffect> pool, Vector3 position, float damage)
        {

            this.pool = pool;
            transform.position = position;
            this.damage = damage;
            elapsed = 0f;
            hitDone = false;
            released = false;
        }

        private void Update()
        {
            if (pool == null || released)
            {
                return;
            }

            elapsed += Time.deltaTime;

            if (hitDelay >= 0f && !hitDone && elapsed >= hitDelay)
            {
                hitDone = true;
                Hit();
            }

            if (releaseDelay >= 0f && elapsed >= releaseDelay)
            {
                Release();
            }
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
            if (released)
            {
                return;
            }

            released = true;
            pool.Release(this);
        }
    }
}
