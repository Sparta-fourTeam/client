namespace Game.Core.Messages
{
    //WaveProgress 발행
    public readonly struct WaveGaugeFilled
    {
        public bool IsFinalWave { get; }

        public WaveGaugeFilled(bool isFinalWave)
        {
            IsFinalWave = isFinalWave;
        }
    }

    //WaveProgress 구독
    public readonly struct WaveStarted
    {
        public int WaveIndex { get; }
        public int EnemyCount { get; }
        public bool IsFinalWave { get; }

        public int MaxEliteCount { get; }
        public int MaxBossCount { get; }

        public WaveStarted(int waveIndex, int enemyCount, bool isFinalWave, 
                            int maxEliteCount = 0, int maxBossCount = 0)
        {
            WaveIndex = waveIndex;
            EnemyCount = enemyCount;
            IsFinalWave = isFinalWave;
            MaxEliteCount = maxEliteCount;
            MaxBossCount = maxBossCount;
        }
    }

    //WaveProgress 발행
    public readonly struct WaveGaugeChanged
    {
        public int WaveIndex { get; }
        public int Current { get; }
        public int Max { get; }

        public WaveGaugeChanged(int waveIndex, int current, int max)
        {
            WaveIndex = waveIndex;
            Current = current;
            Max = max;
        }
    }

    public readonly struct AllEnemiesCleared
    {
    }
}
