using System;

namespace Game.Core
{
    /// <summary>PlayerLevels.json. 레벨 L에서 L+1로 오르는 데 필요한 경험치는 BaseExp + StepExp × (L - 1)이다. MaxLevel은 도달 가능한 최고 레벨이다</summary>
    [Serializable]
    public class PlayerLevelConfig
    {
        public int BaseExp;
        public int StepExp;
        public int MaxLevel;

        public PlayerLevelTable ToTable()
        {
            if (BaseExp <= 0 || StepExp < 0 || MaxLevel < 2)
            {
                throw new InvalidOperationException("PlayerLevels: BaseExp는 양수, StepExp는 0 이상, MaxLevel은 2 이상이어야 합니다.");
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
