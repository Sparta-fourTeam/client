namespace Game.Core
{
    public interface IWeaponStats
    {
        float Cooldown { get; }
        float Damage { get; }
        int HitCount { get; } // Legacy alias for ProjectileCount.
        int ProjectileCount { get; }
        int CastCount { get; }
        int PierceCount { get; }
        float ProjectileSpeed { get; }
        float FreezeDuration { get; }
    }
}
