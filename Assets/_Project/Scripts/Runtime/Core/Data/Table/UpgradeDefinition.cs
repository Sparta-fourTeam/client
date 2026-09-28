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

        /// <summary>level(1부터 시작)에서 다음 레벨로 올리는 비용</summary>
        public int CostAt(int level) => LevelCosts[level - 1];
    }
}
