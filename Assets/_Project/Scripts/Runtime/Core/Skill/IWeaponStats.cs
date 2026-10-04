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
        float KnockbackDistance { get; }
        float ExplosionRadius { get; }
        float ExplosionDamage { get; }
        float FrostbiteRatio { get; }
        float ShardFrostbiteRatio { get; }
        int SplitCount { get; }
        float ShardDamageMultiplier { get; }
        float ParalysisDuration { get; }
        float LightningStrikeRatio { get; }
        float AuxiliaryLightningRatio { get; }
        bool AuxiliaryExplosions { get; }
    }
}
