using Game.Core;

namespace Game.View
{
    /// <summary>스테이지 몬스터 강화 안내 문구 한 줄</summary>
    public static class MonsterBonusText
    {
        public static string Line(MonsterStat stat, int percent) => stat switch
        {
            MonsterStat.Hp => L10n.Get("monsterBonus.hp", percent.ToString()),
            MonsterStat.Damage => L10n.Get("monsterBonus.damage", percent.ToString()),
            MonsterStat.Speed => L10n.Get("monsterBonus.speed", percent.ToString()),
            _ => string.Empty,
        };
    }
}
