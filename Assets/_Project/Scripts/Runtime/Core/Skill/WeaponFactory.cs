using UnityEngine;

namespace Game.Core
{
    public static class WeaponFactory
    {
        public static WeaponBase Create(WeaponData data, GameObject prefab, Transform caster, IEnemyTargetProvider targetProvider, Game.Core.Defense.Wall wall = null, SkillConfig config = null)
        {
            config ??= SkillConfig.FromDefinition(data);
            return config.Attack.Type switch
            {
                CastType.Projectile => new ProjectileCaster(data, prefab, caster, targetProvider, wall, config),
                CastType.Hitscan => new HitscanCaster(data, prefab, caster, targetProvider, config),
                _ => throw new System.NotImplementedException()
            };
        }
    }
}
