using UnityEngine;

namespace Game.Core
{
    public static class WeaponFactory
    {
        public static WeaponBase Create(WeaponData data, GameObject prefab, Transform caster)
        {
            return data.castType switch
            {
                CastType.Projectile => new ProjectileCaster(data, prefab, caster),
                CastType.Hitscan => new HitscanCaster(data, caster),
                _ => throw new System.NotImplementedException()
            };
        }
    }
}
