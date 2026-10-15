using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Pool;
using Time = UnityEngine.Time;

namespace Game.Core
{
    public partial class Projectile : MonoBehaviour
    {
        private static readonly ProfilerMarker TickMarker = new("Projectile.Tick");
        private static readonly ProfilerMarker TickReactionsMarker = new("Projectile.TickReactions");
        private static readonly ProfilerMarker QueryTargetsMarker = new("Projectile.QueryTargets");
        private static readonly ProfilerMarker SortTargetsMarker = new("Projectile.SortTargets");
        private static readonly ProfilerMarker ResolveHitsMarker = new("Projectile.ResolveHits");
        private static readonly ProfilerMarker SpawnImpactMarker = new("Projectile.SpawnImpact");
        private static readonly ProfilerMarker RecordHitMarker = new("Projectile.RecordHit");
        private static readonly ProfilerMarker ApplyHitMarker = new("Projectile.ApplyHit");
        private static readonly ProfilerMarker HitReactionsMarker = new("Projectile.HitReactions");
        private static readonly ProfilerMarker KillReactionsMarker = new("Projectile.KillReactions");
        private static readonly ProfilerMarker ExpiredReactionsMarker = new("Projectile.ExpiredReactions");
        private static readonly ProfilerMarker ClearReservationMarker = new("Projectile.ClearReservation");
        private static readonly ProfilerMarker ReleaseMarker = new("Projectile.Release");

        /// <summary>적에게 맞았을 때 맞은 자리에 생성하는 임팩트(선택). 임팩트 프리팹이 스스로 사라지게 만든다 (파티클 Stop Action을 Destroy로)</summary>
        [SerializeField] private GameObject impactPrefab;
        [SerializeField] private ProjectileVisual.FormSprite[] formSprites;
        [SerializeField] private GameObject baseVisual;

        private ProjectileVisual visual;
        private GameObject activeImpact;
        public void SetVisualForm(SkillForm form)
        {
            visual ??= new ProjectileVisual(gameObject, formSprites, baseVisual);
            visual.SetForm(form);
            activeImpact = impactPrefab;
            if (formSprites == null) { return; }
            foreach (var entry in formSprites)
            {
                if (entry.form == form && entry.impactPrefab != null) { activeImpact = entry.impactPrefab; break; }
            }
        }
        private void OnDisable() => ClearReservation();
        private void OnDestroy() { ClearReservation(); visual?.Dispose(); }

        private IObjectPool<Projectile> pool;
        private Vector3 direction;
        private float speed;
        private float lifetime;
        private float elapsed;
        private AttackReactions hitReactions;
        private System.Func<float> randomValue;
        private AttackReactions reactions;
        private bool released;
        private TargetReservations.Reservation reservation;
        private Game.Core.Combat.DamageSource sourceSkill;   // 이 투사체를 만든 스킬 (피해 집계와 속성 전달용)
        private IEnemyTarget ignoredTarget;
        private IEnemyTargetProvider targetProvider;
        private readonly ProjectileHitLedger hitLedger = new();
        private readonly List<IEnemyTarget> hitBuffer = new List<IEnemyTarget>(HitCandidateCount);
        private System.Comparison<IEnemyTarget> compareHits;
        private Vector2 hitSegmentStart, hitSegmentEnd;
        // 적 스프라이트가 레퍼런스 크기로 줄어든 것(슬라임 가로 약 0.6, 콜라이더 반지름 약 0.3)에 맞춘 값. 이전에는 1.4짜리 적에 맞춘 0.7이었다
        private float hitRadius = 0.3f;
        private const int HitCandidateCount = 4;

        public void Init(IObjectPool<Projectile> pool, ProjectileSpawnSettings settings)
        {
            compareHits ??= CompareHitsAlongSegment;
            ClearReservation();
            reservation = settings.Reservation;
            sourceSkill = Game.Core.Combat.DamageAttribution.CurrentSource;
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
            if (Time.timeScale > 0f && Time.deltaTime > 0f) { Tick(Time.deltaTime); }
        }

        internal void Tick(float deltaTime)
        {
            using var tickScope = TickMarker.Auto();
            using var scope = Game.Core.Combat.DamageAttribution.Begin(sourceSkill);
            if (released) { return; }
            Vector2 start = transform.position;
            transform.position += direction * speed * deltaTime;
            Vector2 end = transform.position;

            float activeDelta = Mathf.Min(deltaTime, Mathf.Max(0, lifetime));
            lifetime -= deltaTime;
            elapsed += activeDelta;
            using (TickReactionsMarker.Auto())
            {
                reactions.Raise(AttackEvent.Tick, new AttackContext(end, direction, deltaTime: activeDelta, elapsed: elapsed));
            }
            if (lifetime <= 0)
            {
                released = true;
                ClearReservation();
                using (ExpiredReactionsMarker.Auto())
                {
                    reactions.Raise(AttackEvent.Expired, new AttackContext(end, direction, elapsed: elapsed));
                }
                using (ReleaseMarker.Auto())
                {
                    pool.Release(this);
                }
                return;
            }

            // 전체 이동 구간을 검사하므로 끝점에서 가까운 4체로 제한하지 않는다.
            using (QueryTargetsMarker.Auto())
            {
                targetProvider.GetNearest(start, int.MaxValue, hitBuffer);
            }
            hitSegmentStart = start;
            hitSegmentEnd = end;
            using (SortTargetsMarker.Auto())
            {
                hitBuffer.Sort(compareHits);
            }

            using var resolveHitsScope = ResolveHitsMarker.Auto();
            float hitRadiusSqr = hitRadius * hitRadius;
            foreach (var candidate in hitBuffer)
            {
                float fraction = SegmentFraction(start, end, candidate.Position);
                Vector2 hitPosition = Vector2.Lerp(start, end, fraction);
                if ((hitPosition - candidate.Position).sqrMagnitude > hitRadiusSqr)
                {
                    continue;
                }

                if (candidate.IsDead) { continue; }
                if (ReferenceEquals(candidate, ignoredTarget)) { continue; }
                using (RecordHitMarker.Auto())
                {
                    if (!hitLedger.TryHit(candidate)) { continue; }
                }
                // 적중했거나 더 진행할 수 없는 공격은 피해/자식 시전 콜백 전에 자기 예약을 해제한다.
                if (ReferenceEquals(candidate, reservation?.Target) || hitLedger.Exhausted || candidate.BlocksPierce)
                { ClearReservation(); }
                using (ApplyHitMarker.Auto())
                {
                    hitReactions.Raise(AttackEvent.Hit, new AttackContext(hitPosition, direction, candidate, randomValue));
                }
                SpawnImpact(hitPosition);
                var context = new AttackContext(hitPosition, direction, candidate);
                using (HitReactionsMarker.Auto())
                {
                    reactions.Raise(AttackEvent.Hit, context);
                }
                if (candidate.IsDead)
                {
                    using (KillReactionsMarker.Auto())
                    {
                        reactions.Raise(AttackEvent.Kill, context);
                    }
                }
                if (candidate.BlocksPierce) { hitLedger.Exhaust(); }
                if (hitLedger.Exhausted)
                {
                    Release();
                    break;
                }
            }
        }

        private void Release()
        {
            using var releaseScope = ReleaseMarker.Auto();
            released = true;
            ClearReservation();
            pool.Release(this);
        }

        private void ClearReservation()
        {
            using var reservationScope = ClearReservationMarker.Auto();
            reservation?.Dispose();
            reservation = null;
        }

        private static float SegmentFraction(Vector2 start, Vector2 end, Vector2 point)
        {
            Vector2 segment = end - start;
            float lengthSquared = segment.sqrMagnitude;
            return lengthSquared > 0 ? Mathf.Clamp01(Vector2.Dot(point - start, segment) / lengthSquared) : 0;
        }

        private int CompareHitsAlongSegment(IEnemyTarget a, IEnemyTarget b) =>
            SegmentFraction(hitSegmentStart, hitSegmentEnd, a.Position).CompareTo(
                SegmentFraction(hitSegmentStart, hitSegmentEnd, b.Position));

        private void SpawnImpact(Vector2 position)
        {
            var prefab = activeImpact != null ? activeImpact : impactPrefab;
            if (prefab == null) { return; }
            using var impactScope = SpawnImpactMarker.Auto();
            var impact = Instantiate(prefab, position, transform.rotation);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(impact, gameObject.scene);
            impact.transform.localScale *= Mathf.Clamp(Mathf.Abs(transform.lossyScale.x), .5f, 1.6f);
        }

        private void ApplyDirectionRoration()
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle - 90);
        }
    }
}
