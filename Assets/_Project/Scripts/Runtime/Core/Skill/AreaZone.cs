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
        /// <summary>메인 대상을 공격할 때마다 늘어나 광선이 끝날 때 닿는 추가 피해 비율의 최대값</summary>
        public float FocusRampMax { get; }
        /// <summary>메인 대상 주변 폭발 반경과 피해 비율(공격 한 번 피해 대비)</summary>
        public float FocusBlastRadius { get; }
        public float FocusBlastRatio { get; }
        /// <summary>광선이 메인 대상에서 꺾여 다른 적으로 이어지는 횟수</summary>
        public int Refractions { get; }

        public AreaSettings(float radius, float duration, float pulseInterval, float moveSpeed = 0, float pull = 0, float length = 0, float width = 0,
            float focusBonus = 0, float baseDamage = 0, float focusRampMax = 0, float focusBlastRadius = 0, float focusBlastRatio = 0, int refractions = 0)
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
            FocusRampMax = focusRampMax;
            FocusBlastRadius = focusBlastRadius;
            FocusBlastRatio = focusBlastRatio;
            Refractions = refractions;
        }

        public static AreaSettings From(AreaStats area) =>
            new AreaSettings(area.Radius, area.Duration, area.PulseInterval, area.MoveSpeed, area.Pull);

        /// <summary>광선: 지속 시간 동안 공격 횟수(pulses)만큼 고르게 피해를 준다</summary>
        public static AreaSettings From(BeamStats beam, float baseDamage = 0) =>
            new AreaSettings(0, beam.Duration, beam.Duration / Mathf.Max(1f, beam.Pulses), length: beam.Length, width: beam.Width,
                focusBonus: beam.FocusBonus, baseDamage: baseDamage, focusRampMax: beam.FocusRampMax,
                focusBlastRadius: beam.FocusBlastRadius, focusBlastRatio: beam.FocusBlastRatio, refractions: beam.Refractions);
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
        private float radius, duration, pulseInterval, moveSpeed, pull, length, width, focusBonus, baseDamage, focusRampMax, focusBlastRadius, focusBlastRatio;
        private int mainHits, refractions;
        private readonly List<Vector2> path = new();
        private readonly List<IEnemyTarget> visited = new();
        private readonly List<Transform> segmentVisuals = new();
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
            focusRampMax = Mathf.Max(0, settings.FocusRampMax);
            focusBlastRadius = Mathf.Max(0, settings.FocusBlastRadius);
            focusBlastRatio = Mathf.Max(0, settings.FocusBlastRatio);
            mainHits = 0;
            refractions = Mathf.Max(0, settings.Refractions);
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
            if (IsLine) { TrackAim(); BuildPath(transform.position); }
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
                if (path.Count < 2) { BuildPath(transform.position); }
                for (int i = 0; i < path.Count - 1; i++)
                {
                    var segment = SegmentVisual(i);
                    segment.gameObject.SetActive(true);
                    Vector2 from = path[i], to = path[i + 1];
                    Vector2 along = to - from;
                    float segmentLength = along.magnitude;
                    float angle = Mathf.Atan2(along.y, along.x) * Mathf.Rad2Deg;
                    segment.position = from + along * .5f;
                    segment.rotation = Quaternion.Euler(0, 0, angle);
                    segment.localScale = new Vector3(segmentLength, width, 1);
                }
                for (int i = Mathf.Max(0, path.Count - 1); i < segmentVisuals.Count; i++) { segmentVisuals[i].gameObject.SetActive(false); }
            }
            else
            {
                visual.localScale = Vector3.one * (radius * 2f);
            }
        }

        // 굴절한 구간마다 광선 모양이 하나씩 필요하다. 첫 구간은 프리팹의 visual을 쓰고 나머지는 그것을 복제해 둔다
        private Transform SegmentVisual(int index)
        {
            if (index == 0) { return visual; }
            while (segmentVisuals.Count < index) { segmentVisuals.Add(Instantiate(visual, visual.parent)); }
            return segmentVisuals[index - 1];
        }

        // 광선이 지나는 꺾은선. 굴절이 없으면 시전 위치에서 방향으로 길이만큼 곧게 뻗는다.
        // 굴절이 있으면 겨눈 적에서 끝나고, 거기서 가장 가까운 다른 적으로 굴절 횟수만큼 이어진다 (겨눈 적이 죽었거나 길이 밖이면 곧게 뻗는다)
        private void BuildPath(Vector2 origin)
        {
            path.Clear();
            path.Add(origin);
            if (refractions > 0 && aim != null && !aim.IsDead && (aim.Position - origin).sqrMagnitude <= length * length)
            {
                path.Add(aim.Position);
                visited.Clear();
                visited.Add(aim);
                Vector2 current = aim.Position;
                for (int i = 0; i < refractions; i++)
                {
                    var next = NearestUnvisited(current);
                    if (next == null) { break; }
                    path.Add(next.Position);
                    visited.Add(next);
                    current = next.Position;
                }
                return;
            }
            path.Add(origin + beamDirection * length);
        }

        private IEnemyTarget NearestUnvisited(Vector2 from)
        {
            provider.GetNearest(from, int.MaxValue, candidates);
            foreach (var target in candidates)
            {
                if (target == null || target.IsDead || visited.Contains(target)) { continue; }
                if ((target.Position - from).sqrMagnitude > length * length) { return null; }
                return target;
            }
            return null;
        }

        // 선(광선) 위에 있는지: 꺾은선의 어느 구간과 적 위치의 거리가 폭의 절반 이하인지
        private bool OnBeam(Vector2 point)
        {
            float limit = width * width * .25f;
            for (int i = 0; i < path.Count - 1; i++)
            {
                Vector2 from = path[i], along = path[i + 1] - from;
                float lengthSquared = along.sqrMagnitude;
                float t = lengthSquared > 0 ? Mathf.Clamp01(Vector2.Dot(point - from, along) / lengthSquared) : 0;
                if ((point - from - along * t).sqrMagnitude <= limit) { return true; }
            }
            return false;
        }

        // 광선이 겨눈 메인 대상에게만 일어나는 일 (집중 광선): 초점의 추가 피해, 초점 조정으로 늘어나는 추가 피해, 집중 폭파
        private void HitMain(IEnemyTarget target, AttackContext context)
        {
            mainHits++;
            // 추가 피해는 광선이 끝날 때까지 메인 대상을 계속 맞힌다고 보고 공격 횟수에 비례해 최대값까지 늘어난다 (증가 속도는 원문에 없어 임시)
            float totalPulses = Mathf.Max(1f, Mathf.Round(duration / pulseInterval));
            float ramp = focusRampMax * Mathf.Min(1f, mainHits / totalPulses);
            float extra = baseDamage * (focusBonus + ramp);
            // 메인 대상 추가 피해는 같은 적중의 일부다. 충격(IsImpact)으로 주면 타격 횟수형 방어막이 한 번의 광선 공격을 두 번 맞은 것으로 센다
            if (extra > 0) { new DamageReaction(extra).Execute(context); }
            if (focusBlastRadius > 0 && focusBlastRatio > 0)
            {
                SkillReactionEffects.Explode(provider, target.Position, focusBlastRadius, baseDamage * focusBlastRatio);
            }
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
                    if (!OnBeam(target.Position)) { continue; }
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
                if (IsLine && baseDamage > 0 && ReferenceEquals(target, aim)) { HitMain(target, context); }
                reactions.Raise(AttackEvent.Hit, context);
                if (target.IsDead) { reactions.Raise(AttackEvent.Kill, context); }
            }
        }
    }
}
