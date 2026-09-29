using UnityEngine;

namespace Game.Core
{
    public abstract class WeaponBase
    {
        protected WeaponData data;
        protected Transform caster;
        public int Level { get; private set; } = 1;
        protected float cooldownTimer = 0;

        protected float currentCooldown;
        protected float currentDamage;
        protected int currentHitCount;




        public WeaponBase(WeaponData data, Transform caster)
        {
            this.data = data;
            this.caster = caster;
            var stats = data.baseStats;

            currentCooldown = stats.TotalCooldown();
            currentDamage = stats.TotalDamage();
            currentHitCount = stats.hitCount;
        }

        public void Tick()
        {
            cooldownTimer -= Time.deltaTime;

            if (cooldownTimer <= 0)
            {
                OnFire();
                cooldownTimer = currentCooldown;
            }
        }

        protected abstract void OnFire();

        public void LevelUp(WeaponUpgradeOption option)
        {
            if (Level >= data.maxLevel)
            {
                return;
            }

            Level++;

            switch (option.type)
            {
                case UpgradeType.AttackSpeed:
                    currentCooldown = Mathf.Max(0.1f, currentCooldown - option.value * 0.01f);
                    break;
                case UpgradeType.Damage:
                    currentDamage += option.value;
                    break;
                case UpgradeType.ProjectileCount:
                    currentHitCount += Mathf.RoundToInt(option.value);
                    break;
            }
        }
    }
}
