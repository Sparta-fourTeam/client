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
        public float FreezeDuration => data.freezeDuration;
        public float KnockbackDistance => data.knockbackDistance;
        public float FrostbiteRatio => 0;
        public float ShardFrostbiteRatio => 0;
        public int SplitCount => 0;
        public float ShardDamageMultiplier => 1;

    }
}
