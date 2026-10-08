namespace Game.Core
{
    public class EnemySpawnConfig
    {
        public float SpawnIntervalMin { get; }
        public float SpawnIntervalMax { get; }
        public float SpawnCooldown { get; }

        public EnemySpawnConfig(
            float spawnIntervalMin,
            float spawnIntervalMax,
            float spawnCooldown)
        {
            SpawnIntervalMin = spawnIntervalMin;
            SpawnIntervalMax = spawnIntervalMax;
            SpawnCooldown = spawnCooldown;
        }

        public static EnemySpawnConfig From(SpawnDefinition spawn) =>
            new EnemySpawnConfig(spawn.IntervalMin, spawn.IntervalMax, spawn.Cooldown);
    }
}
