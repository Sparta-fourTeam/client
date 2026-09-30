using System.Collections.Generic;

namespace Game.Core
{
    public enum CastType { Projectile, Hitscan }

    public class WeaponData
    {
        public int id;
        public string name;
        public string desc;
        public CastType castType;
        public WeaponBaseStats baseStats;
        public List<WeaponUpgradeOption> upgrades;
        public int maxLevel;

    }

    public class WeaponBaseStats
    {
        public float cooldown;
        public float baseDamage;
        public float range;
        public float speed;
        public int hitCount;
    }

    public enum UpgradeType { AttackSpeed, Damage, ProjectileCount, HitCount }

    public class StatEffect
    {
        public UpgradeType type;
        public float value;
    }

    public class WeaponUpgradeOption
    {
        public string id;
        public string name;
        public string desc;
        public List<StatEffect> effects;
        public int maxPickCount;
        public string[] requiredCardIds;
        public int[] requiredWeaponIds;
    }
}
