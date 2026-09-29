using System.Collections.Generic;

namespace Game.Core
{
    public class WeaponData
    {
        public int id;
        public string name;
        public string desc;
        public WeaponBaseStats baseStats;
        public List<WeaponUpgradeOption> upgrades;
        public int maxLevel;

    }

    public class WeaponBaseStats
    {
        public float cooldown;
        public float cooldownMultiple;
        public float baseDamage;
        public float bonusDamage;
        public float damageMultiple;
        public float speed;
        public int hitCount;

        public float TotalCooldown()
        {
            return (1 + cooldownMultiple * 0.01f) * cooldown;
        }

        public float TotalDamage()
        {
            return (baseDamage + bonusDamage) * damageMultiple;
        }
    }

    public enum UpgradeType { AttackSpeed, Damage, ProjectileCount, }
    public class WeaponUpgradeOption
    {
        public string name;
        public string desc;
        public UpgradeType type;
        public float value;
    }
}
