namespace Game.Core
{
    public class EnemySpawnConfig
    {
        public float SpawnIntervalMin { get; }
        public float SpawnIntervalMax { get; }
        public float SpawnCooldown { get; }
        public EnemyType EnemyType { get; }

        public EnemySpawnConfig(
            float spawnIntervalMin,
            float spawnIntervalMax,
            float spawnCooldown,
            EnemyType enemyType)
        {
            SpawnIntervalMin = spawnIntervalMin;
            SpawnIntervalMax = spawnIntervalMax;
            SpawnCooldown = spawnCooldown;
            EnemyType = enemyType;
        }
    }
}
