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

        private LineRenderer triangleOutline;
        private Material triangleMaterial;
        private SpriteRenderer originalSprite;
        private bool originalSpriteEnabled;

        public void SetVisualForm(WeaponForm form)
        {
            if (originalSprite == null)
            {
                originalSprite = GetComponent<SpriteRenderer>();
                if (originalSprite != null) { originalSpriteEnabled = originalSprite.enabled; }
            }
            bool triangle = form == WeaponForm.TriangleIce;
            if (triangle && triangleOutline == null)
            {
                triangleOutline = gameObject.AddComponent<LineRenderer>();
                var shader = Shader.Find("Sprites/Default");
                if (shader != null) { triangleMaterial = new Material(shader); triangleOutline.sharedMaterial = triangleMaterial; }
                triangleOutline.useWorldSpace = false; triangleOutline.loop = true; triangleOutline.positionCount = 3;
                triangleOutline.startWidth = triangleOutline.endWidth = .12f;
                triangleOutline.startColor = triangleOutline.endColor = new Color(.35f, .85f, 1);
                triangleOutline.sortingOrder = originalSprite != null ? originalSprite.sortingOrder : 10;
                triangleOutline.SetPosition(0, new Vector3(0, 1.2f, 0));
                triangleOutline.SetPosition(1, new Vector3(-.8f, -.7f, 0));
                triangleOutline.SetPosition(2, new Vector3(.8f, -.7f, 0));
            }
            if (originalSprite != null) { originalSprite.enabled = !triangle && originalSpriteEnabled; }
            if (triangleOutline != null) { triangleOutline.enabled = triangle; }
        }

        private void OnDestroy()
        {
            if (triangleMaterial != null)
            {
                if (Application.isPlaying) { Destroy(triangleMaterial); }
                else { DestroyImmediate(triangleMaterial); }
            }
        }

        private IObjectPool<Projectile> pool;
        private Vector3 direction;
        private float damage;
        private float speed;
        private float lifetime;
        private float freezeDuration;
        private float burnDuration;
        private float burnDamage;
        private float burnMaxHpRatio;
        private System.Action<Vector2> burnOnDeath;
        private float paralysisDuration;
        private float paralysisChance;
        private float freezeChance;
        private float frostbiteChance;
        private float burnChance;
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
        private float hitRadius = 0.3f;
        private const int HitCandidateCount = 4;

        public void Init(IObjectPool<Projectile> pool, Vector3 startPos, Vector3 direction, float damage, float speed, float lifetime, IEnemyTargetProvider targetProvider, int pierceCount = 0, float freezeDuration = 0, System.Action<Vector2, Vector3, IEnemyTarget> onHit = null, IEnemyTarget ignoredTarget = null, float knockbackDistance = 0, float frostbiteRatio = 0, float paralysisDuration = 0, float lightningDamage = 0, float paralysisChance = 1, System.Func<float> randomValue = null, float burnDuration = 0, float burnDamage = 0, float burnMaxHpRatio = 0, System.Action<Vector2> burnOnDeath = null, float freezeChance = 1, float frostbiteChance = 1, float burnChance = 1, float collisionRadius = .3f)
        {
            hitRadius = Mathf.Max(0, collisionRadius);
            this.onHit = onHit;
            this.ignoredTarget = ignoredTarget;
            this.pool = pool;
            transform.position = startPos;
            this.direction = direction.normalized;
            this.damage = damage;
            this.speed = speed;
            this.lifetime = lifetime;
            this.freezeDuration = freezeDuration;
            this.burnDuration = burnDuration;
            this.burnDamage = burnDamage;
            this.burnMaxHpRatio = burnMaxHpRatio;
            this.burnOnDeath = burnOnDeath;
            this.paralysisDuration = paralysisDuration;
            this.paralysisChance = paralysisChance;
            this.freezeChance = freezeChance;
            this.frostbiteChance = frostbiteChance;
            this.burnChance = burnChance;
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
                candidate.TakeDamage((int)damage);
                if (freezeDuration > 0 && candidate is IFreezableTarget freezable && StatusProc.Roll(freezeChance, randomValue)) { freezable.ApplyFreeze(freezeDuration); }
                if (knockbackDistance > 0 && candidate is IKnockbackTarget movable) { movable.ApplyKnockback(direction, knockbackDistance); }
                if (frostbiteRatio > 0 && candidate is IFrostbiteTarget frosted && StatusProc.Roll(frostbiteChance, randomValue)) { frosted.ApplyFrostbite(damage * frostbiteRatio); }
                if (paralysisDuration > 0 && candidate is IParalyzableTarget paralyzed && StatusProc.Roll(paralysisChance, randomValue)) { paralyzed.ApplyParalysis(paralysisDuration); }
                if (lightningDamage > 0) { candidate.TakeDamage(Mathf.Max(1, (int)lightningDamage)); }
                if (burnDuration > 0 && burnDamage > 0 && candidate is IBurnableTarget burning && StatusProc.Roll(burnChance, randomValue)) { burning.ApplyBurn(burnDamage, burnDuration, burnMaxHpRatio, burnOnDeath); }
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
