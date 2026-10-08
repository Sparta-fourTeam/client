using System;
using System.Collections.Generic;

namespace Game.Core.Messages
{
    public readonly struct WaveCompleted
    {
        public int WaveIndex { get; }
        public bool IsFinalWave { get; }

        public WaveCompleted(int waveIndex, bool isFinalWave)
        {
            WaveIndex = waveIndex;
            IsFinalWave = isFinalWave;
        }
    }

    //WaveProgress, EnemySpawner 구독
    public readonly struct WaveStarted
    {
        public int WaveIndex { get; }

        /// <summary>웨이브를 끝내려면 처치해야 하는 수</summary>
        public int EnemyCount { get; }
        public bool IsFinalWave { get; }

        /// <summary>이 웨이브에 나오는 몬스터 구성. 처치 수만 세는 구독자(WaveProgress)는 쓰지 않아 비어 있을 수 있다</summary>
        public IReadOnlyList<WaveSpawn> Spawns { get; }

        /// <summary>구성 없이 처치 수만 알리는 웨이브. 스폰은 일어나지 않는다</summary>
        public WaveStarted(int waveIndex, int enemyCount, bool isFinalWave)
        {
            WaveIndex = waveIndex;
            EnemyCount = enemyCount;
            IsFinalWave = isFinalWave;
            Spawns = Array.Empty<WaveSpawn>();
        }

        /// <summary>몬스터 구성이 있는 웨이브. 처치 수는 마릿수 합계다</summary>
        public WaveStarted(int waveIndex, bool isFinalWave, IReadOnlyList<WaveSpawn> spawns)
        {
            WaveIndex = waveIndex;
            IsFinalWave = isFinalWave;
            Spawns = spawns ?? Array.Empty<WaveSpawn>();
            int total = 0;
            foreach (var spawn in Spawns) { total += spawn.Count; }
            EnemyCount = total;
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
