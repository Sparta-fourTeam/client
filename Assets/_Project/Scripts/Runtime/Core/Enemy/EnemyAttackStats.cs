using System;

namespace Game.Core
{
    public readonly struct EnemyAttackStats
    {
        private readonly int _burstCount;

        public AttackType Type { get; }
        public int Damage { get; }
        public float Interval { get; }
        public float Range { get; }
        public float ProjectileSpeed { get; }

        /// <summary>공격 한 번에 연달아 나가는 횟수. 1이면 연속 공격이 아니다</summary>
        public int BurstCount => _burstCount < 1 ? 1 : _burstCount;

        /// <summary>연속 공격에서 다음 타격까지의 간격(초). BurstCount가 1이면 쓰지 않는다</summary>
        public float BurstInterval { get; }

        public EnemyAttackStats(AttackType type, int damage, float interval, float range, float projectileSpeed = 0f,
            int burstCount = 1, float burstInterval = 0f)
        {
            if (damage < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(damage));
            }

            if (interval <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(interval), "적 공격 간격은 0보다 커야 합니다.");
            }

            if (range < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(range));
            }

            if (type == AttackType.Ranged && projectileSpeed <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(projectileSpeed), "원거리 적의 투사체 속도는 0보다 커야 합니다.");
            }

            if (burstCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(burstCount), "연속 공격 횟수는 1 이상이어야 합니다.");
            }

            if (burstCount > 1 && (burstInterval <= 0f || interval <= burstInterval * (burstCount - 1)))
            {
                throw new ArgumentOutOfRangeException(nameof(burstInterval),
                    "연속 공격 간격은 0보다 크고, 연속 공격이 한 번의 공격 간격 안에 끝나야 합니다.");
            }

            Type = type;
            Damage = damage;
            Interval = interval;
            Range = range;
            ProjectileSpeed = projectileSpeed;
            _burstCount = burstCount;
            BurstInterval = burstCount > 1 ? burstInterval : 0f;
        }
    }
}
