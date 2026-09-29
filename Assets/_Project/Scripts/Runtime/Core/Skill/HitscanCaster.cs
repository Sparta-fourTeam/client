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
            // TODO: 실제 명중 판정/데미지 적용은 충돌 시스템 붙인 뒤 구현
            Debug.Log($"OnFire() [Hitscan] Damage: {stats.Damage}, HitCount: {stats.HitCount}");
        }
    }
}
