using Game.Core;

namespace Game.View
{
    /// <summary>스테이지 몬스터 강화 안내 문구 한 줄</summary>
    public static class MonsterBonusText
    {
        public static string Line(MonsterStat stat, int percent) => stat switch
        {
            MonsterStat.Hp => $"요마의 체력이 {percent}% 증가합니다",
            MonsterStat.Damage => $"요마의 공격력이 {percent}% 증가합니다",
            MonsterStat.Speed => $"요마의 이동 속도가 {percent}% 증가합니다",
            _ => string.Empty,
        };
    }
}
