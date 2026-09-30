namespace Game.Core.Messages
{
    public readonly struct EnemyDied
    {
        public int EnemyId { get; }

        public EnemyDied(int enemyId)
        {
            EnemyId = enemyId;
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
