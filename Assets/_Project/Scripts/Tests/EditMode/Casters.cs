using Game.Core;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>테스트에서 공격 종류를 지정해 시전기를 만든다 (데이터의 castType과 무관하게 전략을 고른다).</summary>
    internal static class Casters
    {
        public static SkillCaster Projectile(WeaponData data, GameObject prefab, Transform caster, IEnemyTargetProvider targets,
            Game.Core.Defense.Wall wall = null, SkillConfig config = null) =>
            WeaponFactory.Create(data, caster, targets, new ProjectileStrategy(prefab, wall), config);

        public static SkillCaster Hitscan(WeaponData data, GameObject prefab, Transform caster, IEnemyTargetProvider targets,
            SkillConfig config = null) =>
            WeaponFactory.Create(data, caster, targets, new HitscanStrategy(prefab), config);

        public static ProjectileStrategy Projectile(SkillCaster weapon) => (ProjectileStrategy)weapon.Strategy;
        public static HitscanStrategy Hitscan(SkillCaster weapon) => (HitscanStrategy)weapon.Strategy;
    }
}
