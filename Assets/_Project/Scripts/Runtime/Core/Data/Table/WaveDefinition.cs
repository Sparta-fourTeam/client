using System;

namespace Game.Core
{
    /// <summary>스테이지 한 웨이브의 스폰 계획</summary>
    [Serializable]
    public class WaveDefinition
    {
        public int EnemyCount;
        public int MaxEliteCount;
        public int MaxBossCount;
    }
}
