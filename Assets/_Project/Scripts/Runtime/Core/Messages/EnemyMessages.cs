using Game.Core.Combat;

namespace Game.Core.Messages
{
    public readonly struct EnemyDied
    {
        public int EnemyId { get; }

        /// <summary>true면 분열·소환으로 생긴 적이다. 웨이브 마릿수에 포함되지 않아 웨이브 게이지에 세지 않는다</summary>
        public bool IsSummoned { get; }

        public EnemyType Type { get; }

        public EnemyDied(int enemyId, bool isSummoned = false, EnemyType type = EnemyType.Normal)
        {
            EnemyId = enemyId;
            IsSummoned = isSummoned;
            Type = type;
        }
    }

    public readonly struct EnemyHpChanged
    {
        public int EnemyId { get; }
        public int Current { get; }
        public int Max { get; }

        /// <summary>실제 HP 차감량과 피해 출처. 회복 또는 기존 생성자는 기본값이다.</summary>
        public DamageInfo Damage { get; }

        public EnemyHpChanged(int enemyId, int current, int max) : this(enemyId, current, max, default) { }

        public EnemyHpChanged(int enemyId, int current, int max, DamageInfo damage)
        {
            EnemyId = enemyId;
            Current = current;
            Max = max;
            Damage = damage;
        }
    }
}
