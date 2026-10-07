namespace Game.Core.Messages
{
    public readonly struct EnemyDied
    {
        public int EnemyId { get; }

        /// <summary>true면 분열·소환으로 생긴 적이다. 웨이브 마릿수에 포함되지 않아 웨이브 게이지에 세지 않는다</summary>
        public bool IsSummoned { get; }

        public EnemyDied(int enemyId, bool isSummoned = false)
        {
            EnemyId = enemyId;
            IsSummoned = isSummoned;
        }
    }

    public readonly struct EnemyHpChanged
    {
        public int EnemyId { get; }
        public int Current { get; }
        public int Max { get; }

        public EnemyHpChanged(int enemyId, int current, int max)
        {
            EnemyId = enemyId;
            Current = current;
            Max = max;
        }
    }
}
