namespace Game.Core
{
    public class BaseWeaponStats : IWeaponStats
    {
        private readonly WeaponBaseStats data;
        public BaseWeaponStats(WeaponBaseStats data) => this.data = data;

        public float Cooldown => data.cooldown;
        public float Damage => data.baseDamage;
        public int HitCount => ProjectileCount;
        public int ProjectileCount => System.Math.Max(1, data.hitCount);
        public int CastCount => System.Math.Max(1, data.castCount);
        public int PierceCount => System.Math.Max(0, data.pierceCount);
        public float ProjectileSpeed => data.speed;
        public float ProjectileSizeMultiplier => 1;
        public float FreezeDuration => data.freezeDuration;
        public float KnockbackDistance => data.knockbackDistance;
        public float ExplosionRadius => data.explosionRadius;
        public float ExplosionDamage => data.baseDamage * data.explosionDamageRatio;
        public float FrostbiteRatio => 0;
        public float ShardFrostbiteRatio => 0;
        public int SplitCount => 0;
        public float ShardDamageMultiplier => 1;

        public float ParalysisDuration => data.paralysisDuration;
        public float AuxiliaryParalysisDuration => 0;
        public float KillLightningRatio => 0;
        public float LightningStrikeRatio => 0;
        public float AuxiliaryLightningRatio => 0;
        public bool AuxiliaryExplosions => false;
        public float BurnDuration => 0;
        public float BurnRatio => 0;
        public float BurnMaxHpRatio => 0;
        public bool BurnDeathExplosion => false;
        public float FieldDuration => 0;
        public float FieldDamageRatio => data.fieldDamageRatio;
        public float FieldFlatDamage => 0;
        public float FieldDamageMultiplier => 1;
        public float FieldRadius => data.fieldRadius;
        public float FieldSlowRatio => data.fieldSlowRatio;
        public float StunDuration => data.stunDuration;
        public float SlowDuration => data.slowDuration;
        public float SlowRatio => data.slowRatio;
        public float VulnerabilityRatio => 0;
        public float VulnerabilityDuration => 0;
        public int ReserveCastCount => 0;
        public WeaponForm Form => WeaponForm.Default;
    }
}
