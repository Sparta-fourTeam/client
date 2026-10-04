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
    }
}
