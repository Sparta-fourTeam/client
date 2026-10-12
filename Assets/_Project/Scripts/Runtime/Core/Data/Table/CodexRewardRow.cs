using System;

namespace Game.Core
{
    /// <summary>도감 등록 보상 한 줄. 몬스터마다가 아니라 등급(Normal·Elite·Boss)별로 정한다</summary>
    [Serializable]
    public class CodexRewardRow
    {
        /// <summary>EnemyType 이름</summary>
        public string Grade;
        public int Gold;

        public EnemyType GetGrade()
        {
            if (!Enum.TryParse(Grade, out EnemyType grade) || !Enum.IsDefined(typeof(EnemyType), grade))
            {
                throw new InvalidOperationException($"CodexRewards: 알 수 없는 등급입니다: {Grade}");
            }

            return grade;
        }

        public void Validate()
        {
            GetGrade();
            if (Gold <= 0)
            {
                throw new InvalidOperationException($"CodexRewards {Grade}: Gold는 1 이상이어야 합니다");
            }
        }
    }
}
