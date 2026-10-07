namespace Game.Core
{
    /// <summary>스킬이 열려 있는지 알려 준다. 전투 중 새 스킬 카드는 열린 스킬만 나온다</summary>
    public interface ISkillUnlock
    {
        bool IsUnlocked(SkillData skill);
    }
}
