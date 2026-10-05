using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>스테이지 테이블 한 행</summary>
    [Serializable]
    public class StageDefinition
    {
        public int Id;
        public int ClearGold;

        /// <summary>rating 1~3 클리어 시 1회성 보상 (index 0 = rating 1)</summary>
        public int[] RatingRewards;

        public int WallHp;
        public SpawnDefinition Spawn;

        /// <summary>진행 순서대로의 웨이브. 마지막 웨이브가 최종 웨이브다</summary>
        public List<WaveDefinition> Waves;

        public void Validate()
        {
            if (WallHp <= 0)
            {
                throw new InvalidOperationException($"Stages {Id}: WallHp는 1 이상이어야 합니다");
            }

            if (Spawn == null || Spawn.IntervalMin <= 0f || Spawn.IntervalMax < Spawn.IntervalMin || Spawn.Cooldown < 0f)
            {
                throw new InvalidOperationException($"Stages {Id}: Spawn 간격이 잘못되었습니다 (0 < IntervalMin <= IntervalMax, Cooldown >= 0)");
            }

            if (Waves == null || Waves.Count == 0)
            {
                throw new InvalidOperationException($"Stages {Id}: 웨이브가 하나도 없습니다");
            }

            foreach (var wave in Waves)
            {
                if (wave == null || wave.EnemyCount <= 0 || wave.MaxEliteCount < 0 || wave.MaxBossCount < 0)
                {
                    throw new InvalidOperationException($"Stages {Id}: 웨이브 값이 잘못되었습니다 (EnemyCount >= 1, 엘리트·보스 수 >= 0)");
                }
            }
        }
    }
}
