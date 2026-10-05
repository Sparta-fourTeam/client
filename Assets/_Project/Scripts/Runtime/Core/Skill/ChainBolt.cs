using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace Game.Core
{
    /// <summary>연쇄 공격 하나의 진행. 첫 대상을 맞히고, 간격(HopInterval)마다 현재 대상에서 가장 가까운 아직 맞지 않은 적으로
    /// 튕기며(반사 횟수만큼) 각 대상에 적중 반응을 건다. 새 대상에 도착할 때마다 Bounce, 끝나면 Expired를 알린다.</summary>
    public readonly struct ChainSettings
    {
        public int Bounces { get; }
        public float JumpRange { get; }
        public float HopInterval { get; }
        /// <summary>0보다 크면 튕기는 경로 위(이 폭 이내)의 적도 함께 공격한다</summary>
        public float PathWidth { get; }

        public ChainSettings(int bounces, float jumpRange, float hopInterval, float pathWidth = 0)
        {
            Bounces = bounces;
            JumpRange = jumpRange;
            HopInterval = hopInterval;
            PathWidth = pathWidth;
        }

        public static ChainSettings From(ChainStats chain) => new ChainSettings(chain.Bounces, chain.JumpRange, chain.HopInterval, chain.PathWidth);
    }

    public sealed class ChainBolt : MonoBehaviour
    {
        [SerializeField] private LineRenderer line;

        private IObjectPool<ChainBolt> pool;
        private IEnemyTargetProvider provider;
        private AttackReactions hitReactions = AttackReactions.Empty;
        private AttackReactions reactions = AttackReactions.Empty;
        private Func<float> randomValue;
        private ChainSettings settings;
        private readonly HashSet<IEnemyTarget> visited = new();
        private readonly List<IEnemyTarget> candidates = new();
        private readonly List<Vector3> points = new();
        private IEnemyTarget current;
        private Vector2 position;
        private int remaining;
        private float sinceHop;
        private bool started, released;

        /// <param name="origin">번개가 나가는 위치(시전자)</param>
        /// <param name="first">첫 대상</param>
        public void Init(IObjectPool<ChainBolt> pool, IEnemyTargetProvider provider, Vector2 origin, IEnemyTarget first,
            ChainSettings settings, AttackReactions hitReactions, AttackReactions reactions, Func<float> randomValue = null)
        {
            this.pool = pool;
            this.provider = provider;
            this.settings = settings;
            this.hitReactions = hitReactions ?? AttackReactions.Empty;
            this.reactions = reactions ?? AttackReactions.Empty;
            this.randomValue = randomValue;
            visited.Clear();
            points.Clear();
            points.Add(origin);
            current = first;
            position = origin;
            remaining = Mathf.Max(0, settings.Bounces);
            sinceHop = 0;
            started = false;
            released = false;
            transform.position = origin;
            RefreshLine();
            this.reactions.Raise(AttackEvent.Start, new AttackContext(origin, first != null ? (Vector3)(first.Position - origin).normalized : Vector3.up));
        }

        private void Update() => Tick(Time.deltaTime);

        internal void Tick(float deltaTime)
        {
            if (released || pool == null) { return; }
            if (deltaTime < 0 || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) { throw new ArgumentOutOfRangeException(nameof(deltaTime)); }
            if (!started)
            {
                started = true;
                if (current == null || current is EnemyModel dead && dead.IsDead) { Finish(); return; }
                Arrive(current, first: true);
            }
            sinceHop += deltaTime;
            float interval = Mathf.Max(.0001f, settings.HopInterval);
            while (!released && sinceHop + 0.0000001f >= interval)
            {
                sinceHop -= interval;
                if (remaining <= 0) { Finish(); break; }
                var next = FindNext();
                if (next == null) { Finish(); break; }
                remaining--;
                Vector2 from = position;
                if (settings.PathWidth > 0) { HitAlongPath(from, next.Position); }
                Arrive(next, first: false);
            }
            if (!released && remaining <= 0 && started) { Finish(); }
        }

        // 현재 대상에서 가장 가까운, 아직 맞지 않은 살아 있는 적. 튕김 거리 밖이면 연쇄가 끝난다.
        private IEnemyTarget FindNext()
        {
            candidates.Clear();
            provider.GetNearest(position, int.MaxValue, candidates);
            IEnemyTarget best = null;
            float bestSquared = settings.JumpRange * settings.JumpRange;
            foreach (var candidate in candidates)
            {
                if (candidate == null || visited.Contains(candidate) || candidate is EnemyModel model && model.IsDead) { continue; }
                float distanceSquared = (candidate.Position - position).sqrMagnitude;
                if (distanceSquared <= bestSquared) { best = candidate; bestSquared = distanceSquared; }
            }
            return best;
        }

        private void HitAlongPath(Vector2 from, Vector2 to)
        {
            candidates.Clear();
            provider.GetNearest(from, int.MaxValue, candidates);
            Vector2 segment = to - from;
            float lengthSquared = segment.sqrMagnitude;
            if (lengthSquared <= 0) { return; }
            float halfWidthSquared = settings.PathWidth * settings.PathWidth * .25f;
            var onPath = new List<IEnemyTarget>();
            foreach (var candidate in candidates)
            {
                if (candidate == null || visited.Contains(candidate) || candidate.Position == to || candidate is EnemyModel model && model.IsDead) { continue; }
                float t = Mathf.Clamp01(Vector2.Dot(candidate.Position - from, segment) / lengthSquared);
                if ((candidate.Position - (from + segment * t)).sqrMagnitude <= halfWidthSquared) { onPath.Add(candidate); }
            }
            Vector3 direction = segment.normalized;
            foreach (var target in onPath)
            {
                var context = new AttackContext(target.Position, direction, target, randomValue);
                hitReactions.Raise(AttackEvent.Hit, context);
                reactions.Raise(AttackEvent.Hit, context);
                if (target is EnemyModel killed && killed.IsDead) { reactions.Raise(AttackEvent.Kill, context); }
            }
        }

        private void Arrive(IEnemyTarget target, bool first)
        {
            Vector2 from = position;
            visited.Add(target);
            current = target;
            position = target.Position;
            points.Add(position);
            RefreshLine();
            Vector3 direction = (position - from).sqrMagnitude > 0 ? (Vector3)(position - from).normalized : Vector3.up;
            var context = new AttackContext(position, direction, target, randomValue);
            hitReactions.Raise(AttackEvent.Hit, context);
            reactions.Raise(AttackEvent.Hit, context);
            if (!first)
            {
                hitReactions.Raise(AttackEvent.Bounce, context);
                reactions.Raise(AttackEvent.Bounce, context);
            }
            if (target is EnemyModel killed && killed.IsDead) { reactions.Raise(AttackEvent.Kill, context); }
        }

        private void RefreshLine()
        {
            if (line == null) { return; }
            line.positionCount = points.Count;
            for (int i = 0; i < points.Count; i++) { line.SetPosition(i, points[i]); }
        }

        private void Finish()
        {
            if (released) { return; }
            released = true;
            reactions.Raise(AttackEvent.Expired, new AttackContext(position, Vector3.up));
            pool.Release(this);
        }
    }
}
