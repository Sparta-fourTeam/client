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
        private IEnemyTargetProvider targetProvider;
        private readonly List<IEnemyTarget> hitBuffer = new List<IEnemyTarget>(HitCandidateCount);
        // 적 스프라이트가 레퍼런스 크기로 줄어든 것(슬라임 가로 약 0.6, 콜라이더 반지름 약 0.3)에 맞춘 값. 이전에는 1.4짜리 적에 맞춘 0.7이었다
        private const float HitRadius = 0.3f;
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
            ApplyDirectionRoration();
        }

        /// <summary>선분 from→to 위에서 point에 가장 가까운 지점의 위치 비율(0~1)을 돌려준다. 선분이 점이면 0</summary>
        public static float ClosestPointRatio(Vector2 from, Vector2 to, Vector2 point)
        {
            Vector2 segment = to - from;
            float lengthSqr = segment.sqrMagnitude;
            if (lengthSqr <= Mathf.Epsilon)
            {
                return 0f;
            }

            return Mathf.Clamp01(Vector2.Dot(point - from, segment) / lengthSqr);
        }

        private void Update()
        {
            Vector2 previous = transform.position;
            transform.position += direction * speed * Time.deltaTime;

            lifetime -= Time.deltaTime;
            if (lifetime <= 0)
            {
                pool.Release(this);
                return;
            }

            // 프레임이 낮으면 한 프레임에 판정 지름보다 멀리 가서 적을 통과하므로, 끝점이 아니라 이번 프레임에 지나온 구간 전체로 맞았는지 본다
            Vector2 current = transform.position;
            targetProvider.GetNearest((previous + current) * 0.5f, HitCandidateCount, hitBuffer);

            float hitRadiusSqr = HitRadius * HitRadius;
            IEnemyTarget firstHit = null;
            float firstHitRatio = float.MaxValue;
            foreach (var candidate in hitBuffer)
            {
                float ratio = ClosestPointRatio(previous, current, candidate.Position);
                Vector2 closest = Vector2.Lerp(previous, current, ratio);
                if ((closest - candidate.Position).sqrMagnitude > hitRadiusSqr)
                {
                    continue;
                }

                // 한 프레임에 여러 적이 걸리면 먼저 지나친 적을 맞힌다
                if (ratio < firstHitRatio)
                {
                    firstHit = candidate;
                    firstHitRatio = ratio;
                }
            }

            if (firstHit != null)
            {
                Vector2 hitPoint = Vector2.Lerp(previous, current, firstHitRatio);
                transform.position = new Vector3(hitPoint.x, hitPoint.y, transform.position.z);
                firstHit.TakeDamage((int)damage);
                SpawnImpact();
                pool.Release(this);
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
