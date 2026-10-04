namespace Game.Core
{
    public class StatDecorator : IWeaponStats
    {
        protected readonly IWeaponStats inner;
        protected StatDecorator(IWeaponStats inner) => this.inner = inner;

        public virtual float Cooldown => inner.Cooldown;
        public virtual float Damage => inner.Damage;
        public virtual int HitCount => ProjectileCount;
        public virtual int ProjectileCount => inner.ProjectileCount;
        public virtual int CastCount => inner.CastCount;
        public virtual int PierceCount => inner.PierceCount;
        public virtual float ProjectileSpeed => inner.ProjectileSpeed;
        public virtual float FreezeDuration => inner.FreezeDuration;
        public virtual float KnockbackDistance => inner.KnockbackDistance;
        public virtual float ExplosionRadius => inner.ExplosionRadius;
        public virtual float ExplosionDamage => inner.ExplosionDamage;
        public virtual float FrostbiteRatio => inner.FrostbiteRatio;
        public virtual float ShardFrostbiteRatio => inner.ShardFrostbiteRatio;
        public virtual int SplitCount => inner.SplitCount;
        public virtual float ShardDamageMultiplier => inner.ShardDamageMultiplier;
        public virtual float ParalysisDuration => inner.ParalysisDuration;
        public virtual float AuxiliaryParalysisDuration => inner.AuxiliaryParalysisDuration;
        public virtual float KillLightningRatio => inner.KillLightningRatio;
        public virtual float LightningStrikeRatio => inner.LightningStrikeRatio;
        public virtual float AuxiliaryLightningRatio => inner.AuxiliaryLightningRatio;
        public virtual bool AuxiliaryExplosions => inner.AuxiliaryExplosions;
        public virtual float BurnDuration => inner.BurnDuration;
        public virtual float BurnRatio => inner.BurnRatio;
        public virtual float BurnMaxHpRatio => inner.BurnMaxHpRatio;
        public virtual bool BurnDeathExplosion => inner.BurnDeathExplosion;
        public virtual float FieldDuration => inner.FieldDuration;
        public virtual float FieldDamageRatio => inner.FieldDamageRatio;
        public virtual float FieldFlatDamage => inner.FieldFlatDamage;
        public virtual float FieldDamageMultiplier => inner.FieldDamageMultiplier;
        public virtual float FieldRadius => inner.FieldRadius;
        public virtual float FieldSlowRatio => inner.FieldSlowRatio;
        public virtual WeaponForm Form => inner.Form;
    }
}
