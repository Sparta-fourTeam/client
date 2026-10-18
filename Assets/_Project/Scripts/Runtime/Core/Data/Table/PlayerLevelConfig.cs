using System;

namespace Game.Core
{
    /// <summary>PlayerLevels.json. 레벨 L에서 L+1로 오르는 데 필요한 경험치는 BaseExp + StepExp × (L - 1)이다. MaxLevel은 도달 가능한 최고 레벨이다</summary>
    [Serializable]
    public class PlayerLevelConfig
    {
        // 경험치 밸런스가 아니라 테이블 할당 크기에 대한 클라이언트/API 계약 상한이다.
        public const int MaxSupportedLevel = 1000;

        public int BaseExp;
        public int StepExp;
        public int MaxLevel;

        public PlayerLevelTable ToTable()
        {
            if (BaseExp <= 0 || StepExp < 0 || MaxLevel < 2 || MaxLevel > MaxSupportedLevel)
            {
                throw new InvalidOperationException($"PlayerLevels: BaseExp는 양수, StepExp는 0 이상, MaxLevel은 2 이상 {MaxSupportedLevel} 이하여야 합니다.");
            }

            long count = MaxLevel - 1L;
            long requiredExp = count * BaseExp + count * (count - 1) / 2 * StepExp;
            if (requiredExp > int.MaxValue)
            {
                throw new InvalidOperationException("PlayerLevels: 최고 레벨의 누적 경험치가 저장 범위를 초과합니다.");
            }

            var rows = new PlayerLevelRow[MaxLevel - 1];
            for (int i = 0; i < rows.Length; i++)
            {
                rows[i] = new PlayerLevelRow { Level = i + 1, RequiredExp = checked(BaseExp + StepExp * i) };
            }

            return new PlayerLevelTable(rows);
        }
    }
}
