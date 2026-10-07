namespace Game.Core
{
    /// <summary>플레이어 레벨이 SkillData.unlockLevel 이상이면 스킬이 열린 것으로 본다 (SkillUnlockRule과 같은 기준).</summary>
    public sealed class ProfileSkillUnlock : ISkillUnlock
    {
        private readonly PlayerProfile profile;
        public ProfileSkillUnlock(PlayerProfile profile) => this.profile = profile;
        public bool IsUnlocked(SkillData skill) => skill.unlockLevel <= profile.Level;
    }
}
