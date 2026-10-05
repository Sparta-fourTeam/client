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
        float ProjectileSizeMultiplier { get; }
        float FreezeDuration { get; }
        float KnockbackDistance { get; }
        float ExplosionRadius { get; }
        float ExplosionDamage { get; }
        float FrostbiteRatio { get; }
        float ShardFrostbiteRatio { get; }
        int SplitCount { get; }
        float ShardDamageMultiplier { get; }
        float ParalysisDuration { get; }
        float AuxiliaryParalysisDuration { get; }
        float KillLightningRatio { get; }
        float LightningStrikeRatio { get; }
        float AuxiliaryLightningRatio { get; }
        bool AuxiliaryExplosions { get; }
        float BurnDuration { get; }
        float BurnRatio { get; }
        float BurnMaxHpRatio { get; }
        bool BurnDeathExplosion { get; }
        float FieldDuration { get; }
        float FieldDamageRatio { get; }
        float FieldFlatDamage { get; }
        float FieldDamageMultiplier { get; }
        float FieldRadius { get; }
        float FieldSlowRatio { get; }
        float StunDuration { get; }
        float SlowDuration { get; }
        float SlowRatio { get; }
        float VulnerabilityRatio { get; }
        float VulnerabilityDuration { get; }
        int ReserveCastCount { get; }
        WeaponForm Form { get; }
    }
}
