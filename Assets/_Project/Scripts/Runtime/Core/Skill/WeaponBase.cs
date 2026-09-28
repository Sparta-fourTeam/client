using UnityEngine;

namespace Game.Core
{
    public abstract class WeaponBase
    {
        protected WeaponData data;
        protected Transform owner;
        public int Level { get; private set; } = 1;
        protected float cooldownTimer = 0;

        protected float currentCooldown;
        protected float currentDamage;
        protected int currentHitCount;




        public WeaponBase(WeaponData data, Transform owner)
        {
            this.data = data;
            this.owner = owner;
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

        public void LevelUp()
        {
            if (Level < data.maxLevel) { Level++; }
        }
    }
}
