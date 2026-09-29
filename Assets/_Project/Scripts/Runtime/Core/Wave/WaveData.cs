namespace Game.Core
{
    public class WaveData
    {
        public float SpawnIntervalMin { get; }
        public float SpawnIntervalMax { get; }
        public int SpawnCount { get; }
        public float SpawnCooldown { get; }
        public EnemyType EnemyType { get; }

        public WaveData(
            float spawnIntervalMin,
            float spawnIntervalMax,
            int spawnCount,
            float spawnCooldown,
            EnemyType enemyType)
        {
            SpawnIntervalMin = spawnIntervalMin;
            SpawnIntervalMax = spawnIntervalMax;
            SpawnCount = spawnCount;
            SpawnCooldown = spawnCooldown;
            EnemyType = enemyType;
        }
    }
}
