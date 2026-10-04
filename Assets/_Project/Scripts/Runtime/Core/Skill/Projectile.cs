using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Time = UnityEngine.Time;

namespace Game.Core
{
    public class Projectile : MonoBehaviour
    {
        /// <summary>적에게 맞았을 때 맞은 자리에 생성하는 임팩트(선택). 임팩트 프리팹이 스스로 사라지게 만든다 (파티클 Stop Action을 Destroy로)</summary>
        [SerializeField] private GameObject impactPrefab;

        private IObjectPool<Projectile> pool;
        private Vector3 direction;
        private float damage;
        private float speed;
        private float lifetime;
        private float freezeDuration;
        private IEnemyTargetProvider targetProvider;
        private readonly ProjectileHitLedger hitLedger = new();
        private readonly List<IEnemyTarget> hitBuffer = new List<IEnemyTarget>(HitCandidateCount);
        // 적 스프라이트가 레퍼런스 크기로 줄어든 것(슬라임 가로 약 0.6, 콜라이더 반지름 약 0.3)에 맞춘 값. 이전에는 1.4짜리 적에 맞춘 0.7이었다
        private const float HitRadius = 0.3f;
        private const int HitCandidateCount = 4;

        public void Init(IObjectPool<Projectile> pool, Vector3 startPos, Vector3 direction, float damage, float speed, float lifetime, IEnemyTargetProvider targetProvider, int pierceCount = 0, float freezeDuration = 0)
        {
            this.pool = pool;
            transform.position = startPos;
            this.direction = direction.normalized;
            this.damage = damage;
            this.speed = speed;
            this.lifetime = lifetime;
            this.freezeDuration = freezeDuration;
            this.targetProvider = targetProvider;
            hitLedger.Reset(pierceCount);
            ApplyDirectionRoration();
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

                if (!hitLedger.TryHit(candidate)) { continue; }
                candidate.TakeDamage((int)damage);
                if (freezeDuration > 0 && candidate is IFreezableTarget freezable) { freezable.ApplyFreeze(freezeDuration); }
                SpawnImpact();
                if (hitLedger.Exhausted)
                {
                    pool.Release(this);
                    break;
                }
            }
        }

        private void SpawnImpact()
        {
            if (impactPrefab != null)
            {
                Instantiate(impactPrefab, transform.position, Quaternion.identity);
            }
        }

        private void ApplyDirectionRoration()
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle - 90);
        }
    }
}
