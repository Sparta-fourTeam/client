using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Time = UnityEngine.Time;

namespace Game.Core
{
    public class Projectile : MonoBehaviour
    {
        private IObjectPool<Projectile> pool;
        private Vector3 direction;
        private float damage;
        private float speed;
        private float lifetime;
        private IEnemyTargetProvider targetProvider;
        private readonly List<IEnemyTarget> hitBuffer = new List<IEnemyTarget>(HitCandidateCount);
        private const float HitRadius = 0.7f;
        private const int HitCandidateCount = 4;

        public void Init(IObjectPool<Projectile> pool, Vector3 startPos, Vector3 direction, float damage, float speed, float lifetime, IEnemyTargetProvider targetProvider)
        {
            this.pool = pool;
            transform.position = startPos;
            this.direction = direction.normalized;
            this.damage = damage;
            this.speed = speed;
            this.lifetime = lifetime;
            this.targetProvider = targetProvider;
        }

        private void Update()
        {
            transform.position += direction * speed * Time.deltaTime;

            lifetime -= Time.deltaTime;
            if (lifetime <= 0)
            {
                pool.Release(this);
                return;
            }

            targetProvider.GetNearest(transform.position, HitCandidateCount, hitBuffer);

            float hitRadiusSqr = HitRadius * HitRadius;
            foreach (var candidate in hitBuffer)
            {
                if (((Vector2)transform.position - candidate.Position).sqrMagnitude > hitRadiusSqr)
                {
                    continue;
                }

                Debug.Log(candidate);
                candidate.TakeDamage((int)damage);
                pool.Release(this);
                break;
            }
        }
    }
}
