using System;

namespace Game.Core
{
    /// <summary>업그레이드 테이블 한 행</summary>
    [Serializable]
    public class UpgradeDefinition
    {
        public string UpgradeId;
        public int MaxLevel;
        public int[] LevelCosts;

        /// <summary>level(1부터 시작)에 도달하기 위한 비용. 아직 구매하지 않은 상태(레벨 0)에서 첫 구매는 CostAt(1)이다</summary>
        public int CostAt(int level) => LevelCosts[level - 1];
    }
}
