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
        private float paralysisDuration;
        private float paralysisChance;
        private System.Func<float> randomValue;
        private float lightningDamage;
        private float knockbackDistance;
        private float frostbiteRatio;
        private System.Action<Vector2, Vector3, IEnemyTarget> onHit;
        private IEnemyTarget ignoredTarget;
        private IEnemyTargetProvider targetProvider;
        private readonly ProjectileHitLedger hitLedger = new();
        private readonly List<IEnemyTarget> hitBuffer = new List<IEnemyTarget>(HitCandidateCount);
        // 적 스프라이트가 레퍼런스 크기로 줄어든 것(슬라임 가로 약 0.6, 콜라이더 반지름 약 0.3)에 맞춘 값. 이전에는 1.4짜리 적에 맞춘 0.7이었다
        private const float HitRadius = 0.3f;
        private const int HitCandidateCount = 4;

        public void Init(IObjectPool<Projectile> pool, Vector3 startPos, Vector3 direction, float damage, float speed, float lifetime, IEnemyTargetProvider targetProvider, int pierceCount = 0, float freezeDuration = 0, System.Action<Vector2, Vector3, IEnemyTarget> onHit = null, IEnemyTarget ignoredTarget = null, float knockbackDistance = 0, float frostbiteRatio = 0, float paralysisDuration = 0, float lightningDamage = 0, float paralysisChance = 1, System.Func<float> randomValue = null)
        {
            this.onHit = onHit;
            this.ignoredTarget = ignoredTarget;
            this.pool = pool;
            transform.position = startPos;
            this.direction = direction.normalized;
            this.damage = damage;
            this.speed = speed;
            this.lifetime = lifetime;
            this.freezeDuration = freezeDuration;
            this.paralysisDuration = paralysisDuration;
            this.paralysisChance = Mathf.Clamp01(paralysisChance);
            this.randomValue = randomValue ?? (() => Random.value);
            this.lightningDamage = lightningDamage;
            this.knockbackDistance = knockbackDistance;
            this.frostbiteRatio = frostbiteRatio;
            this.targetProvider = targetProvider;
            hitLedger.Reset(pierceCount);
            ApplyDirectionRoration();
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        internal void Tick(float deltaTime)
        {
            Vector2 start = transform.position;
            transform.position += direction * speed * deltaTime;
            Vector2 end = transform.position;

            lifetime -= deltaTime;
            if (lifetime <= 0)
            {
                pool.Release(this);
                return;
            }

            // 전체 이동 구간을 검사하므로 끝점에서 가까운 4체로 제한하지 않는다.
            targetProvider.GetNearest(start, int.MaxValue, hitBuffer);
            hitBuffer.Sort((a, b) => SegmentFraction(start, end, a.Position).CompareTo(SegmentFraction(start, end, b.Position)));

            float hitRadiusSqr = HitRadius * HitRadius;
            foreach (var candidate in hitBuffer)
            {
                float fraction = SegmentFraction(start, end, candidate.Position);
                Vector2 hitPosition = Vector2.Lerp(start, end, fraction);
                if ((hitPosition - candidate.Position).sqrMagnitude > hitRadiusSqr)
                {
                    continue;
                }

                if (ReferenceEquals(candidate, ignoredTarget)) { continue; }
                if (!hitLedger.TryHit(candidate)) { continue; }
                candidate.TakeDamage((int)damage);
                if (freezeDuration > 0 && candidate is IFreezableTarget freezable) { freezable.ApplyFreeze(freezeDuration); }
                if (knockbackDistance > 0 && candidate is IKnockbackTarget movable) { movable.ApplyKnockback(direction, knockbackDistance); }
                if (frostbiteRatio > 0 && candidate is IFrostbiteTarget frosted) { frosted.ApplyFrostbite(damage * frostbiteRatio); }
                if (paralysisDuration > 0 && candidate is IParalyzableTarget paralyzed && this.randomValue() < paralysisChance) { paralyzed.ApplyParalysis(paralysisDuration); }
                if (lightningDamage > 0) { candidate.TakeDamage(Mathf.Max(1, (int)lightningDamage)); }
                SpawnImpact(hitPosition);
                onHit?.Invoke(hitPosition, direction, candidate);
                if (hitLedger.Exhausted)
                {
                    pool.Release(this);
                    break;
                }
            }
        }

        private static float SegmentFraction(Vector2 start, Vector2 end, Vector2 point)
        {
            Vector2 segment = end - start;
            float lengthSquared = segment.sqrMagnitude;
            return lengthSquared > 0 ? Mathf.Clamp01(Vector2.Dot(point - start, segment) / lengthSquared) : 0;
        }

        private void SpawnImpact(Vector2 position)
        {
            if (impactPrefab != null)
            {
                Instantiate(impactPrefab, position, Quaternion.identity);
            }
        }

        private void ApplyDirectionRoration()
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle - 90);
        }
    }
}
