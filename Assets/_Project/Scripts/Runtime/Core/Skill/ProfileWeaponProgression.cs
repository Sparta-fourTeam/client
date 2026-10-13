namespace Game.Core
{
    /// <summary>로비에서 확보한 플레이어 스냅샷의 영구 성장 레벨을 조회한다.</summary>
    public sealed class ProfileWeaponProgression : IWeaponProgression
    {
        private readonly PlayerProfile profile;
        public ProfileWeaponProgression(PlayerProfile profile) => this.profile = profile;
        public int GetLevel(string progressionId) => string.IsNullOrEmpty(progressionId)
            ? 0 : profile.UpgradeLevel(progressionId);
    }
}
