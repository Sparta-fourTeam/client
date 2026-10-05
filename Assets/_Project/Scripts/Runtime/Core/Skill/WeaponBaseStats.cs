namespace Game.Core
{
    public class WeaponBaseStats
    {
        public float cooldown;
        public float baseDamage;
        public float range;
        public float speed;
        // Legacy hitCount is the number of projectiles/targets per cast, not penetration.
        public int hitCount;
        public int castCount = 1;
        public int pierceCount;
        public float castInterval = 0.1f;
        public float freezeDuration;
        public float paralysisDuration;
        public float paralysisChance = 1;
        public float stunDuration;
        public float stunChance = 1;
        public float slowDuration;
        public float slowRatio;
        public float reserveDistance;
        public float reserveCooldown;
        public float reserveInterval;
        public float freezeChance = 1;
        public float frostbiteChance = 1;
        public float burnChance = 1;
        public float knockbackDistance;
        public float explosionRadius;
        public float explosionDamageRatio;
        public float fieldDamageRatio;
        public float fieldRadius;
        public float fieldSlowRatio;
    }
}
