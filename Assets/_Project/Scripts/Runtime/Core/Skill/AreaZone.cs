using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace Game.Core
{
    /// <summary>영역 하나의 모양과 움직임. 길이(Length)가 0보다 크면 시전 위치에서 대상 방향으로 뻗는 선(광선)이고, 아니면 반경(Radius)의 원이다.</summary>
    public readonly struct AreaSettings
    {
        public float Radius { get; }
        public float Duration { get; }
        public float PulseInterval { get; }
        public float MoveSpeed { get; }
        public float Pull { get; }
        /// <summary>광선의 길이. 0이면 원형 영역이다</summary>
        public float Length { get; }
        /// <summary>광선의 폭</summary>
        public float Width { get; }
        public bool IsLine => Length > 0;
        /// <summary>광선의 메인 대상(겨눈 적)이 공격마다 더 받는 피해 비율</summary>
        public float FocusBonus { get; }
        /// <summary>광선의 공격 한 번 피해. 메인 대상 추가 피해 계산에 쓴다</summary>
        public float BaseDamage { get; }

        public AreaSettings(float radius, float duration, float pulseInterval, float moveSpeed = 0, float pull = 0, float length = 0, float width = 0,
            float focusBonus = 0, float baseDamage = 0)
        {
            Radius = radius;
            Duration = duration;
            PulseInterval = pulseInterval;
            MoveSpeed = moveSpeed;
            Pull = pull;
            Length = length;
            Width = width;
            FocusBonus = focusBonus;
            BaseDamage = baseDamage;
        }

        public static AreaSettings From(AreaStats area) =>
            new AreaSettings(area.Radius, area.Duration, area.PulseInterval, area.MoveSpeed, area.Pull);

        /// <summary>광선: 지속 시간 동안 공격 횟수(pulses)만큼 고르게 피해를 준다</summary>
        public static AreaSettings From(BeamStats beam, float baseDamage = 0) =>
            new AreaSettings(0, beam.Duration, beam.Duration / Mathf.Max(1f, beam.Pulses), length: beam.Length, width: beam.Width,
                focusBonus: beam.FocusBonus, baseDamage: baseDamage);
    }

    /// <summary>지정한 자리에 일정 시간 머물며 주기마다 범위 안의 적에게 적중 반응을 거는 영역(또는 광선).
    /// 시작(Start)·틱(Tick)·적중(Hit)·처치(Kill)·소멸(Expired)을 반응으로 알리므로 자식 스킬 시전을 그대로 붙일 수 있다.</summary>
    public sealed class AreaZone : MonoBehaviour
    {
        [SerializeField] private Transform visual;

        private IObjectPool<AreaZone> pool;
        private IEnemyTargetProvider provider;
        private AttackReactions hitReactions = AttackReactions.Empty;
        private AttackReactions reactions = AttackReactions.Empty;
        private Func<float> randomValue;
        private float radius, duration, pulseInterval, moveSpeed, pull, length, width, focusBonus, baseDamage;
        private float elapsed, sincePulse;
        private Vector2? moveTarget;
        private IEnemyTarget aim;
        private Vector2 beamDirection;
        private bool released;
        private readonly List<IEnemyTarget> candidates = new();

        private bool IsLine => length > 0;
        private Game.Core.Combat.DamageSource sourceSkill;   // 이 장판을 만든 스킬 (피해 집계와 속성 전달용)

        /// <param name="hitReactions">펄스마다 범위 안의 적에게 거는 피해·상태이상</param>
        /// <param name="reactions">강화 카드가 덧붙인 반응(시작·틱·적중·처치·소멸)</param>
        /// <param name="aimTarget">광선이 겨누는 적. 살아 있는 동안 광선이 이 적을 따라간다</param>
        public void Init(IObjectPool<AreaZone> pool, IEnemyTargetProvider provider, Vector2 position, AreaSettings settings,
            AttackReactions hitReactions, AttackReactions reactions, Func<float> randomValue = null, IEnemyTarget aimTarget = null)
        {
            sourceSkill = Game.Core.Combat.DamageAttribution.CurrentSource;
            this.pool = pool;
            this.provider = provider;
            radius = Mathf.Max(0, settings.Radius);
            duration = Mathf.Max(0, settings.Duration);
            pulseInterval = Mathf.Max(.05f, settings.PulseInterval);
            moveSpeed = Mathf.Max(0, settings.MoveSpeed);
            pull = Mathf.Max(0, settings.Pull);
            length = Mathf.Max(0, settings.Length);
            width = Mathf.Max(0, settings.Width);
            focusBonus = Mathf.Max(0, settings.FocusBonus);
            baseDamage = Mathf.Max(0, settings.BaseDamage);
            moveTarget = null;
            aim = aimTarget;
            this.hitReactions = hitReactions ?? AttackReactions.Empty;
            this.reactions = reactions ?? AttackReactions.Empty;
            this.randomValue = randomValue;
            elapsed = 0;
            sincePulse = this.pulseInterval; // 첫 틱에 바로 첫 펄스를 낸다
            released = false;
            transform.position = position;
            beamDirection = aim != null && (aim.Position - position).sqrMagnitude > 0 ? (aim.Position - position).normalized : Vector2.up;
            UpdateVisual();
            this.reactions.Raise(AttackEvent.Start, new AttackContext(position, Direction));
        }

        private Vector3 Direction => IsLine ? (Vector3)beamDirection : Vector3.up;

        private void Update() => Tick(Time.deltaTime);

        internal void Tick(float deltaTime)
        {
            using var scope = Game.Core.Combat.DamageAttribution.Begin(sourceSkill);
            if (released || pool == null) { return; }
            if (deltaTime < 0 || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) { throw new ArgumentOutOfRangeException(nameof(deltaTime)); }
            float active = Mathf.Min(deltaTime, Mathf.Max(0, duration - elapsed));
            elapsed += active;
            if (IsLine) { TrackAim(); }
            else if (moveSpeed > 0 && moveTarget.HasValue)
            {
                transform.position = Vector2.MoveTowards(transform.position, moveTarget.Value, moveSpeed * active);
            }
            Vector2 center = transform.position;
            UpdateVisual();
            reactions.Raise(AttackEvent.Tick, new AttackContext(center, Direction, deltaTime: active, elapsed: elapsed));

            sincePulse += active;
            while (sincePulse + 0.0000001f >= pulseInterval)
            {
                sincePulse -= pulseInterval;
                Pulse(center);
            }

            if (elapsed + 0.0000001f >= duration)
            {
                released = true;
                reactions.Raise(AttackEvent.Expired, new AttackContext(center, Direction, elapsed: elapsed));
                pool.Release(this);
            }
        }

        // 겨눈 적이 살아 있으면 광선이 그 적을 따라간다. 죽으면 마지막 방향을 유지한다.
        private void TrackAim()
        {
            if (aim == null || aim.IsDead) { return; }
            Vector2 toAim = aim.Position - (Vector2)transform.position;
            if (toAim.sqrMagnitude > 0) { beamDirection = toAim.normalized; }
        }

        private void UpdateVisual()
        {
            if (visual == null) { return; }
            if (IsLine)
            {
                float angle = Mathf.Atan2(beamDirection.y, beamDirection.x) * Mathf.Rad2Deg;
                visual.position = (Vector2)transform.position + beamDirection * (length * .5f);
                visual.rotation = Quaternion.Euler(0, 0, angle);
                visual.localScale = new Vector3(length, width, 1);
            }
            else
            {
                visual.localScale = Vector3.one * (radius * 2f);
            }
        }

        // 선(광선) 위에 있는지: 시전 위치에서 방향으로 뻗는 선분과 적 위치의 거리가 폭의 절반 이하인지
        private bool OnBeam(Vector2 origin, Vector2 point)
        {
            Vector2 along = point - origin;
            float t = Mathf.Clamp(Vector2.Dot(along, beamDirection), 0, length);
            return (along - beamDirection * t).sqrMagnitude <= width * width * .25f;
        }

        private void Pulse(Vector2 center)
        {
            candidates.Clear();
            provider.GetNearest(center, int.MaxValue, candidates);
            float radiusSquared = radius * radius;
            float nearestSquared = float.MaxValue;
            moveTarget = null;
            foreach (var target in candidates)
            {
                if (target == null || target.IsDead) { continue; }
                float distanceSquared = (target.Position - center).sqrMagnitude;
                if (IsLine)
                {
                    if (!OnBeam(center, target.Position)) { continue; }
                }
                else
                {
                    if (distanceSquared < nearestSquared) { nearestSquared = distanceSquared; moveTarget = target.Position; }
                    if (distanceSquared > radiusSquared) { continue; }
                    if (pull > 0 && target is IKnockbackTarget pulled && distanceSquared > 0)
                    {
                        pulled.ApplyKnockback(center - target.Position, Mathf.Min(pull, Mathf.Sqrt(distanceSquared)));
                    }
                }
                // 광선이 겨눈 메인 대상은 같은 적중의 충격 피해가 커진다 (집중 광선의 초점). 별도 피해로 주면 타격 횟수형 방어막이 두 번으로 세거나 추가 피해만 통과한다
                float scale = IsLine && focusBonus > 0 && ReferenceEquals(target, aim) ? 1f + focusBonus : 1f;
                var context = new AttackContext(target.Position, Direction, target, randomValue, damageScale: scale);
                hitReactions.Raise(AttackEvent.Hit, context);
                reactions.Raise(AttackEvent.Hit, context);
                if (target.IsDead) { reactions.Raise(AttackEvent.Kill, context); }
            }
        }
    }
}
