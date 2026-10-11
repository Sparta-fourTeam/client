using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>날아가는 중인 공격이 곧 줄 피해를 적마다 기억해, 이미 죽을 만큼 맞을 예정인 적을 다른 스킬이 조준하지 않게 한다.
    /// 예약은 도착 예정 시간이 지나면 사라지므로 빗나가도 오래 남지 않는다.</summary>
    public sealed class TargetReservations
    {
        private readonly struct Entry
        {
            public readonly float Damage;
            public readonly float ExpiresAt;
            public Entry(float damage, float expiresAt) { Damage = damage; ExpiresAt = expiresAt; }
        }

        private readonly Dictionary<IEnemyTarget, List<Entry>> entries = new();
        private readonly List<IEnemyTarget> emptied = new();
        private float now;

        public void Advance(float deltaTime)
        {
            if (deltaTime <= 0) { return; }
            now += deltaTime;
            emptied.Clear();
            foreach (var pair in entries)
            {
                pair.Value.RemoveAll(e => e.ExpiresAt <= now);
                if (pair.Value.Count == 0 || pair.Key == null || pair.Key.IsDead) { emptied.Add(pair.Key); }
            }
            foreach (var target in emptied) { entries.Remove(target); }
        }

        /// <summary>target이 duration초 뒤에 damage만큼 맞을 예정임을 기록한다</summary>
        public void Reserve(IEnemyTarget target, float damage, float duration)
        {
            if (target == null || damage <= 0 || duration <= 0) { return; }
            if (!entries.TryGetValue(target, out var list)) { entries[target] = list = new List<Entry>(); }
            list.Add(new Entry(damage, now + duration));
        }

        /// <summary>예약된 피해가 남은 체력을 넘는 양. 0이면 아직 더 맞아야 죽는 적이다. 체력을 모르는 대상은 항상 0이다</summary>
        public float Overkill(IEnemyTarget target)
        {
            if (target == null || !entries.TryGetValue(target, out var list)) { return 0; }
            float pending = 0;
            foreach (var entry in list) { pending += entry.Damage; }
            return System.Math.Max(0, pending - target.CurrentHp);
        }
    }
}
