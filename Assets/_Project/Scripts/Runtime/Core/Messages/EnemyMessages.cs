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

        /// <summary>저항·취약을 반영한 최종 피해량. 남은 HP를 넘는 과잉 피해도 포함하며 데미지 팝업이 표시한다. 0이면 <see cref="Damage"/>의 차감량을 쓴다.</summary>
        public int DealtDamage { get; }

        public EnemyHpChanged(int enemyId, int current, int max) : this(enemyId, current, max, default) { }

        public EnemyHpChanged(int enemyId, int current, int max, DamageInfo damage, int dealtDamage = 0)
        {
            EnemyId = enemyId;
            Current = current;
            Max = max;
            Damage = damage;
            DealtDamage = dealtDamage;
        }
    }

    /// <summary>엘리트·보스가 나오기 직전에 알리는 경고. 몬스터는 Seconds 뒤에 나타난다</summary>
    public readonly struct SpecialEnemyWarning
    {
        public EnemyType Type { get; }
        public float Seconds { get; }

        public SpecialEnemyWarning(EnemyType type, float seconds)
        {
            Type = type;
            Seconds = seconds;
        }
    }
}
