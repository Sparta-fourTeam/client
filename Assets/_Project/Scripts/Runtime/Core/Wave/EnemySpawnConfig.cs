using UnityEngine;

namespace Game.Core
{
    public class EnemySpawnConfig
    {
        public float SpawnIntervalMin { get; }
        public float SpawnIntervalMax { get; }
        public float SpawnCooldown { get; }
        public float EliteSpawnChance { get; }

        public EnemySpawnConfig(
            float spawnIntervalMin,
            float spawnIntervalMax,
            float spawnCooldown,
            float eliteSpawnChance = 0.2f)
        {
            SpawnIntervalMin = spawnIntervalMin;
            SpawnIntervalMax = spawnIntervalMax;
            SpawnCooldown = spawnCooldown;
            EliteSpawnChance = Mathf.Clamp01(eliteSpawnChance);
        }
    }
}
