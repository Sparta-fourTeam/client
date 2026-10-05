using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace Game.Core
{
    /// <summary>지정한 자리에 일정 시간 머물며 주기마다 범위 안의 적에게 적중 반응을 거는 영역.
    /// 시작(Start)·틱(Tick)·적중(Hit)·처치(Kill)·소멸(Expired)을 반응으로 알리므로 자식 스킬 시전을 그대로 붙일 수 있다.</summary>
    public sealed class AreaZone : MonoBehaviour
    {
        [SerializeField] private Transform visual;

        private IObjectPool<AreaZone> pool;
        private IEnemyTargetProvider provider;
        private AttackReactions hitReactions = AttackReactions.Empty;
        private AttackReactions reactions = AttackReactions.Empty;
        private Func<float> randomValue;
        private float radius, duration, pulseInterval;
        private float elapsed, sincePulse;
        private bool released;
        private readonly List<IEnemyTarget> candidates = new();

        /// <param name="hitReactions">펄스마다 범위 안의 적에게 거는 피해·상태이상</param>
        /// <param name="reactions">강화 카드가 덧붙인 반응(시작·틱·적중·처치·소멸)</param>
        public void Init(IObjectPool<AreaZone> pool, IEnemyTargetProvider provider, Vector2 position, float radius,
            float duration, float pulseInterval, AttackReactions hitReactions, AttackReactions reactions, Func<float> randomValue = null)
        {
            this.pool = pool;
            this.provider = provider;
            this.radius = Mathf.Max(0, radius);
            this.duration = Mathf.Max(0, duration);
            this.pulseInterval = Mathf.Max(.05f, pulseInterval);
            this.hitReactions = hitReactions ?? AttackReactions.Empty;
            this.reactions = reactions ?? AttackReactions.Empty;
            this.randomValue = randomValue;
            elapsed = 0;
            sincePulse = this.pulseInterval; // 첫 틱에 바로 첫 펄스를 낸다
            released = false;
            transform.position = position;
            if (visual != null) { visual.localScale = Vector3.one * (this.radius * 2f); }
            this.reactions.Raise(AttackEvent.Start, new AttackContext(position, Vector3.up));
        }

        private void Update() => Tick(Time.deltaTime);

        internal void Tick(float deltaTime)
        {
            if (released || pool == null) { return; }
            if (deltaTime < 0 || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) { throw new ArgumentOutOfRangeException(nameof(deltaTime)); }
            float active = Mathf.Min(deltaTime, Mathf.Max(0, duration - elapsed));
            elapsed += active;
            Vector2 center = transform.position;
            reactions.Raise(AttackEvent.Tick, new AttackContext(center, Vector3.up, deltaTime: active, elapsed: elapsed));

            sincePulse += active;
            while (sincePulse + 0.0000001f >= pulseInterval)
            {
                sincePulse -= pulseInterval;
                Pulse(center);
            }

            if (elapsed + 0.0000001f >= duration)
            {
                released = true;
                reactions.Raise(AttackEvent.Expired, new AttackContext(center, Vector3.up, elapsed: elapsed));
                pool.Release(this);
            }
        }

        private void Pulse(Vector2 center)
        {
            candidates.Clear();
            provider.GetNearest(center, int.MaxValue, candidates);
            float radiusSquared = radius * radius;
            foreach (var target in candidates)
            {
                if (target == null || target is EnemyModel dead && dead.IsDead) { continue; }
                if ((target.Position - center).sqrMagnitude > radiusSquared) { continue; }
                var context = new AttackContext(target.Position, Vector3.up, target, randomValue);
                hitReactions.Raise(AttackEvent.Hit, context);
                reactions.Raise(AttackEvent.Hit, context);
                if (target is EnemyModel killed && killed.IsDead) { reactions.Raise(AttackEvent.Kill, context); }
            }
        }
    }
}
