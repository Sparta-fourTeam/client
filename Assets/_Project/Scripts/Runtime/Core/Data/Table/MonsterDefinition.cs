using System;

namespace Game.Core
{
    /// <summary>몬스터 테이블 한 행</summary>
    [Serializable]
    public class MonsterDefinition
    {
        public int Id, Exp, Hp, Damage, GaugeValue;
        public float Speed, AttackInterval;
        public bool IsBoss;
    }
}
