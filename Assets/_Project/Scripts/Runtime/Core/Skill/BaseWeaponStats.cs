namespace Game.Core
{
    public class BaseWeaponStats : IWeaponStats
    {
        private readonly WeaponBaseStats data;
        public BaseWeaponStats(WeaponBaseStats data) => this.data = data;

        public float Cooldown => data.cooldown;
        public float Damage => data.baseDamage;
        public int HitCount => data.hitCount;

    }
}
