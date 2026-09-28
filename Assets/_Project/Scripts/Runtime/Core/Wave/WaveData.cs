namespace Game.Core
{
    public class WaveData
    {
        public float SpawnIntervalMin { get; }
        public float SpawnIntervalMax { get; }
        public int SpawnCount { get; }
        public float SpawnCooldown { get; }
        public EnemyType EnemyType { get; }
        public float SpawnPositionXMin { get; }
        public float SpawnPositionXMax { get; }
        public float SpawnPositionY { get; }

        public WaveData(
            float spawnIntervalMin,
            float spawnIntervalMax,
            int spawnCount,
            float spawnCooldown,
            EnemyType enemyType,
            float spawnPositionXMin,
            float spawnPositionXMax,
            float spawnPositionY)
        {
            SpawnIntervalMin = spawnIntervalMin;
            SpawnIntervalMax = spawnIntervalMax;
            SpawnCount = spawnCount;
            SpawnCooldown = spawnCooldown;
            EnemyType = enemyType;
            SpawnPositionXMin = spawnPositionXMin;
            SpawnPositionXMax = spawnPositionXMax;
            SpawnPositionY = spawnPositionY;
        }
    }
}
