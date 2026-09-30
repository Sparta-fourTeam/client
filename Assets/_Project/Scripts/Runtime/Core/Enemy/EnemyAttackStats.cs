using System;

namespace Game.Core
{
    public readonly struct EnemyAttackStats
    {
        public AttackType Type { get; }
        public int Damage { get; }
        public float Interval { get; }
        public float Range { get;  }

        public EnemyAttackStats(AttackType type, int damage, float interval, float range)
        {
            if (damage < 0) throw new ArgumentOutOfRangeException(nameof(damage));
            if (interval <= 0f) throw new ArgumentOutOfRangeException(nameof(interval), "적 공격 간격은 0보다 커야 합니다.");
            if (range < 0) throw new ArgumentOutOfRangeException(nameof(range));

            Type = type;
            Damage = damage;
            Interval = interval;
            Range = range;
        }
    }
}
