using UnityEngine;

namespace Game.Core
{
    public class HitscanCaster : WeaponBase
    {
        public HitscanCaster(WeaponData data, Transform caster) : base(data, caster)
        {
        }

        protected override void OnFire()
        {
            // TODO: 히트스캔 로직 (후속 작업)
        }
    }
}
