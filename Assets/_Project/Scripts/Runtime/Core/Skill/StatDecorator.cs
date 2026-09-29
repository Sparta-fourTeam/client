namespace Game.Core
{
    public class StatDecorator : IWeaponStats
    {
        protected readonly IWeaponStats inner;
        protected StatDecorator(IWeaponStats inner) => this.inner = inner;

        public virtual float Cooldown => inner.Cooldown;
        public virtual float Damage => inner.Damage;
        public virtual int HitCount => inner.HitCount;
    }
}
