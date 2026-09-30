using UnityEngine;

namespace Game.Core
{
    public static class WeaponFactory
    {
        public static WeaponBase Create(WeaponData data, GameObject prefab, Transform caster, IEnemyTargetProvider targetProvider)
        {
            return data.castType switch
            {
                CastType.Projectile => new ProjectileCaster(data, prefab, caster, targetProvider),
                CastType.Hitscan => new HitscanCaster(data, prefab, caster, targetProvider),
                _ => throw new System.NotImplementedException()
            };
        }
    }
}
