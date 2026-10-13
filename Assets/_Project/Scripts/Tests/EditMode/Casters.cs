using Game.Core;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>테스트에서 공격 종류를 지정해 시전기를 만든다 (데이터의 castType과 무관하게 전략을 고른다).</summary>
    /// <summary>자식 시전 요청을 받기만 하는 시전기. 자식 시전 카드를 받아야 하는 테스트에서 연결한다.</summary>
    internal sealed class NoopChildCaster : IChildSkillCaster
    {
        public void Cast(ChildCast cast, AttackContext context) { }
    }

    internal static class Casters
    {
        public static SkillCaster Projectile(SkillData data, GameObject prefab, Transform caster, IEnemyTargetProvider targets,
            Game.Core.Defense.Wall wall = null, SkillConfig config = null) =>
            SkillFactory.Create(data, caster, targets, new ProjectileStrategy(prefab, wall), config);

        public static SkillCaster Hitscan(SkillData data, GameObject prefab, Transform caster, IEnemyTargetProvider targets,
            SkillConfig config = null) =>
            SkillFactory.Create(data, caster, targets, new HitscanStrategy(prefab), config);

        public static ProjectileStrategy Projectile(SkillCaster weapon) => (ProjectileStrategy)weapon.Strategy;
        public static HitscanStrategy Hitscan(SkillCaster weapon) => (HitscanStrategy)weapon.Strategy;
    }
}
