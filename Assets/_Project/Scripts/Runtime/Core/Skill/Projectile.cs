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
        [SerializeField] private ProjectileVisual.FormSprite[] formSprites;
        [SerializeField] private GameObject baseVisual;

        private ProjectileVisual visual;
        public void SetVisualForm(WeaponForm form)
        {
            visual ??= new ProjectileVisual(gameObject, formSprites, baseVisual);
            visual.SetForm(form);
        }
        private void OnDestroy() => visual?.Dispose();

        private IObjectPool<Projectile> pool;
        private Vector3 direction;
        private float speed;
        private float lifetime;
        private float elapsed;
        private AttackReactions hitReactions;
        private System.Func<float> randomValue;
        private AttackReactions reactions;
        private bool released;
        private IEnemyTarget ignoredTarget;
        private IEnemyTargetProvider targetProvider;
        private readonly ProjectileHitLedger hitLedger = new();
        private readonly List<IEnemyTarget> hitBuffer = new List<IEnemyTarget>(HitCandidateCount);
        // 적 스프라이트가 레퍼런스 크기로 줄어든 것(슬라임 가로 약 0.6, 콜라이더 반지름 약 0.3)에 맞춘 값. 이전에는 1.4짜리 적에 맞춘 0.7이었다
        private float hitRadius = 0.3f;
        private const int HitCandidateCount = 4;

        public void Init(IObjectPool<Projectile> pool, ProjectileSpawnSettings settings)
        {
            hitRadius = Mathf.Max(0, settings.CollisionRadius);
            ignoredTarget = settings.IgnoredTarget;
            this.pool = pool;
            transform.position = settings.StartPos;
            direction = settings.Direction.normalized;
            speed = settings.Speed;
            lifetime = settings.Lifetime;
            elapsed = 0;
            targetProvider = settings.TargetProvider;
            hitReactions = settings.HitReactions ?? AttackReactions.Empty;
            randomValue = settings.RandomValue ?? (() => Random.value);
            reactions = settings.Reactions ?? AttackReactions.Empty;
            released = false;
            hitLedger.Reset(settings.PierceCount);
            ApplyDirectionRoration();
            reactions.Raise(AttackEvent.Start, new AttackContext(settings.StartPos, direction));
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        internal void Tick(float deltaTime)
        {
            if (released) { return; }
            Vector2 start = transform.position;
            transform.position += direction * speed * deltaTime;
            Vector2 end = transform.position;

            float activeDelta = Mathf.Min(deltaTime, Mathf.Max(0, lifetime));
            lifetime -= deltaTime;
            elapsed += activeDelta;
            reactions.Raise(AttackEvent.Tick, new AttackContext(end, direction, deltaTime: activeDelta, elapsed: elapsed));
            if (lifetime <= 0)
            {
                released = true;
                reactions.Raise(AttackEvent.Expired, new AttackContext(end, direction, elapsed: elapsed));
                pool.Release(this);
                return;
            }

            // 전체 이동 구간을 검사하므로 끝점에서 가까운 4체로 제한하지 않는다.
            targetProvider.GetNearest(start, int.MaxValue, hitBuffer);
            hitBuffer.Sort((a, b) => SegmentFraction(start, end, a.Position).CompareTo(SegmentFraction(start, end, b.Position)));

            float hitRadiusSqr = hitRadius * hitRadius;
            foreach (var candidate in hitBuffer)
            {
                float fraction = SegmentFraction(start, end, candidate.Position);
                Vector2 hitPosition = Vector2.Lerp(start, end, fraction);
                if ((hitPosition - candidate.Position).sqrMagnitude > hitRadiusSqr)
                {
                    continue;
                }

                if (candidate is EnemyModel enemy && enemy.IsDead) { continue; }
                if (ReferenceEquals(candidate, ignoredTarget)) { continue; }
                if (!hitLedger.TryHit(candidate)) { continue; }
                hitReactions.Raise(AttackEvent.Hit, new AttackContext(hitPosition, direction, candidate, randomValue));
                SpawnImpact(hitPosition);
                var context = new AttackContext(hitPosition, direction, candidate);
                reactions.Raise(AttackEvent.Hit, context);
                if (candidate is EnemyModel killed && killed.IsDead) { reactions.Raise(AttackEvent.Kill, context); }
                if (hitLedger.Exhausted)
                {
                    Release();
                    break;
                }
            }
        }

        private void Release()
        {
            released = true;
            pool.Release(this);
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
