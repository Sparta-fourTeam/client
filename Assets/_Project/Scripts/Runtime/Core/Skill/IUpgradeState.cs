namespace Game.Core
{
    /// <summary>선택 조건 판정에 필요한 전투 중 상태. 미보유 무기 레벨은 0.</summary>
    public interface IUpgradeState
    {
        int GetWeaponLevel(int weaponId);
        int GetPermanentWeaponLevel(int weaponId);
        int GetAcquiredCount(int weaponId, string cardId);
    }
}
