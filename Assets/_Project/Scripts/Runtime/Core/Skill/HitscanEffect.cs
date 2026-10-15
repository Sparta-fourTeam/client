using System;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Pool;

namespace Game.Core
{
    public partial class HitscanEffect : MonoBehaviour
    {
        private static readonly ProfilerMarker TickReactionsMarker = new("HitscanEffect.TickReactions");
        private static readonly ProfilerMarker HitMarker = new("HitscanEffect.Hit");
        private static readonly ProfilerMarker QueryTargetsMarker = new("HitscanEffect.QueryTargets");
        private static readonly ProfilerMarker ResolveHitsMarker = new("HitscanEffect.ResolveHits");
        private static readonly ProfilerMarker ApplyDamageMarker = new("HitscanEffect.ApplyDamage");
        private static readonly ProfilerMarker HitReactionsMarker = new("HitscanEffect.HitReactions");
        private static readonly ProfilerMarker KillReactionsMarker = new("HitscanEffect.KillReactions");
        private static readonly ProfilerMarker ImpactReactionsMarker = new("HitscanEffect.ImpactReactions");
        private static readonly ProfilerMarker ExpiredReactionsMarker = new("HitscanEffect.ExpiredReactions");
        private static readonly ProfilerMarker ReleaseMarker = new("HitscanEffect.Release");

        private static readonly Comparison<Collider2D> CompareDepth = (a, b) => a.transform.position.z.CompareTo(b.transform.position.z);
        private IObjectPool<HitscanEffect> pool;
        [SerializeField] private LayerMask targetMask;
        private float damage;
        private float activeRadius;
        private IEnemyTarget directTarget;
        // 이 타격이 겨냥한 적. Impact 반응이 "방금 맞은 적"으로 쓴다
        private IEnemyTarget sourceTarget;
        private AttackReactions reactions;
        [SerializeField] private float radius;

        // 애니메이션 이벤트(Hit, Release) 대신 시간으로 진행하는 이펙트(파티클 프리팹)용. 0보다 작으면 쓰지 않는다 (기존 프리팹은 그대로)
        [SerializeField] private float hitDelay = -1f;
        [SerializeField] private float releaseDelay = -1f;

        private float elapsed;
        private bool hitDone;
        private bool released;




        private Game.Core.Combat.DamageSource sourceSkill;   // 이 타격을 만든 스킬 (피해 집계와 속성 전달용)

        public void Init(IObjectPool<HitscanEffect> pool, Vector3 position, float damage, IEnemyTarget directTarget = null, AttackReactions reactions = null, IEnemyTarget sourceTarget = null, float impactRadius = -1f)
        {
            sourceSkill = Game.Core.Combat.DamageAttribution.CurrentSource;
            this.pool = pool;
            transform.position = position;
            this.damage = damage;
            activeRadius = impactRadius >= 0 ? impactRadius : radius;
            this.directTarget = directTarget;
            this.sourceTarget = sourceTarget;
            this.reactions = reactions ?? AttackReactions.Empty;
            elapsed = 0f;
            hitDone = false;
            released = false;
            this.reactions.Raise(AttackEvent.Start, new AttackContext(position, Vector3.zero));
        }

        private void Update()
        {
            using var scope = Game.Core.Combat.DamageAttribution.Begin(sourceSkill);
            if (Time.timeScale <= 0f || Time.deltaTime <= 0f || pool == null || released)
            {
                return;
            }

            elapsed += Time.deltaTime;
            using (TickReactionsMarker.Auto())
            {
                reactions.Raise(AttackEvent.Tick, new AttackContext(transform.position, Vector3.zero, deltaTime: Time.deltaTime, elapsed: elapsed));
            }

            if (hitDelay >= 0f && !hitDone && elapsed >= hitDelay)
            {
                Hit();
            }

            if (releaseDelay >= 0f && elapsed >= releaseDelay)
            {
                Release();
            }
        }

        private void Hit()
        {
            using var hitScope = HitMarker.Auto();
            if (!gameObject.activeSelf || hitDone || released)
            {
                return;
            }

            hitDone = true;
            // Secondary lightning hits only its chosen target and has no inherited callbacks.
            if (directTarget != null)
            {
                if (!directTarget.IsDead)
                {
                    using (ApplyDamageMarker.Auto())
                    {
                        directTarget.TakeDamage(Game.Core.Combat.DamageInfo.FromCurrent(Mathf.Max(1, (int)damage), isImpact: true));
                    }
                    var context = new AttackContext(transform.position, Vector3.zero, directTarget);
                    using (HitReactionsMarker.Auto())
                    {
                        reactions.Raise(AttackEvent.Hit, context);
                    }
                    if (directTarget.IsDead)
                    {
                        using (KillReactionsMarker.Auto())
                        {
                            reactions.Raise(AttackEvent.Kill, context);
                        }
                    }
                }
                return;
            }
            // Lease per hit: nested reaction strikes must not overwrite an outer strike's results.
            // Lists grow for dense waves instead of silently truncating a fixed-size collider buffer.
            using var hitsLease = ListPool<Collider2D>.Get(out var hits);
            using var damagedLease = HashSetPool<Enemy>.Get(out var damaged);
            using (QueryTargetsMarker.Auto())
            {
                var filter = new ContactFilter2D();
                filter.SetLayerMask(targetMask);
                filter.useTriggers = Physics2D.queriesHitTriggers;
                Physics2D.OverlapCircle(transform.position, activeRadius, filter, hits);
                // OverlapCircleAll returned ascending Z order; keep reaction order unchanged.
                hits.Sort(CompareDepth);
            }

            using (ResolveHitsMarker.Auto())
            {
                foreach (Collider2D hit in hits)
                {
                    if (hit.TryGetComponent(out Enemy enemy) && !enemy.IsDead && damaged.Add(enemy))
                    {
                        Vector2 position = enemy.transform.position;
                        using (ApplyDamageMarker.Auto())
                        {
                            enemy.TakeDamage(Game.Core.Combat.DamageInfo.FromCurrent((int)damage, isImpact: true));
                        }
                        var context = new AttackContext(position, Vector3.zero, enemy);
                        using (HitReactionsMarker.Auto())
                        {
                            reactions.Raise(AttackEvent.Hit, context);
                        }
                        if (enemy.IsDead)
                        {
                            using (KillReactionsMarker.Auto())
                            {
                                reactions.Raise(AttackEvent.Kill, context);
                            }
                        }
                    }
                }
            }
            using (ImpactReactionsMarker.Auto())
            {
                reactions.Raise(AttackEvent.Impact, new AttackContext(transform.position, Vector3.zero, sourceTarget));
            }
        }

        private void Release()
        {
            using var releaseScope = ReleaseMarker.Auto();
            if (released)
            {
                return;
            }

            released = true;
            using (ExpiredReactionsMarker.Auto())
            {
                reactions.Raise(AttackEvent.Expired, new AttackContext(transform.position, Vector3.zero));
            }
            pool.Release(this);
        }
    }
}
