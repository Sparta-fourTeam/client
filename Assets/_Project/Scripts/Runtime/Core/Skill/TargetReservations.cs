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

        /// <summary>target이 duration초 뒤에 맞을 피해를 기록한다. DamageReaction처럼 매 적중의 소수 부분은 버린다</summary>
        public void Reserve(IEnemyTarget target, float damage, float duration)
        {
            int appliedDamage = (int)damage;
            if (target == null || appliedDamage <= 0 || duration <= 0) { return; }
            if (!entries.TryGetValue(target, out var list)) { entries[target] = list = new List<Entry>(); }
            list.Add(new Entry(appliedDamage, now + duration));
        }

        private float PendingDamage(IEnemyTarget target)
        {
            if (target == null || !entries.TryGetValue(target, out var list)) { return 0; }
            float pending = 0;
            foreach (var entry in list) { pending += entry.Damage; }
            return pending;
        }

        /// <summary>예약된 피해만으로 죽는 대상인지. 남은 체력과 같아도 죽으며, 체력을 모르는 대상은 제외한다</summary>
        public bool IsLethal(IEnemyTarget target)
        {
            if (target == null || target.CurrentHp == int.MaxValue) { return false; }
            float pending = PendingDamage(target);
            return pending > 0 && pending >= target.CurrentHp;
        }

        /// <summary>예약된 피해가 남은 체력을 넘는 양. 정확히 죽는 예약도 0이므로 치명 여부는 IsLethal로 구분한다</summary>
        public float Overkill(IEnemyTarget target)
        {
            if (target == null || target.CurrentHp == int.MaxValue) { return 0; }
            return System.Math.Max(0, PendingDamage(target) - target.CurrentHp);
        }
    }
}
