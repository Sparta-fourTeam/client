using System;

namespace Game.Core
{
    /// <summary>스테이지에서 몬스터 능력치가 늘어나는 항목</summary>
    public enum MonsterStat
    {
        Hp,
        Damage,
        Speed
    }

    /// <summary>스테이지의 몬스터 강화 한 줄 (예: 체력 9% 증가). 한 스테이지에 능력치별로 여러 줄이 함께 있을 수 있다</summary>
    [Serializable]
    public class MonsterBonus
    {
        /// <summary>MonsterStat 이름</summary>
        public string Stat;
        public int Percent;

        public MonsterStat GetStat()
        {
            if (!Enum.TryParse(Stat, out MonsterStat stat) || !Enum.IsDefined(typeof(MonsterStat), stat))
            {
                throw new InvalidOperationException($"MonsterBonuses: 알 수 없는 능력치입니다: {Stat}");
            }

            return stat;
        }
    }
}
