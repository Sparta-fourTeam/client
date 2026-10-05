using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>공격 종류(castType)별 전략을 등록해 두고 시전기를 만든다. 새 공격 종류는 전략을 만들어 여기에 한 줄을 더한다.</summary>
    public static class SkillFactory
    {
        private static readonly Dictionary<CastType, Func<GameObject, Game.Core.Defense.Wall, IAttackStrategy>> Strategies = new()
        {
            [CastType.Projectile] = (prefab, wall) => new ProjectileStrategy(prefab, wall),
            [CastType.Hitscan] = (prefab, wall) => new HitscanStrategy(prefab),
            [CastType.Area] = (prefab, wall) => new AreaStrategy(prefab),
            [CastType.Beam] = (prefab, wall) => new BeamStrategy(prefab),
            [CastType.Chain] = (prefab, wall) => new ChainStrategy(prefab)
        };

        /// <summary>전략이 등록된 공격 종류인지. 카탈로그 검증이 등록되지 않은 종류를 시작 때 막는다</summary>
        public static bool IsRegistered(CastType type) => Strategies.ContainsKey(type);

        public static SkillCaster Create(SkillData data, GameObject prefab, Transform caster, IEnemyTargetProvider targetProvider, Game.Core.Defense.Wall wall = null, SkillConfig config = null)
        {
            config ??= SkillConfig.FromDefinition(data);
            if (!Strategies.TryGetValue(config.Attack.Type, out var create))
            {
                throw new NotSupportedException($"{config.Attack.Type} 공격 전략이 등록되지 않았습니다. WeaponFactory에 등록하세요.");
            }
            return new SkillCaster(data, caster, targetProvider, create(prefab, wall), config);
        }

        /// <summary>전략을 직접 지정해 시전기를 만든다 (공격 종류와 무관한 조합이 필요한 경우)</summary>
        public static SkillCaster Create(SkillData data, Transform caster, IEnemyTargetProvider targetProvider,
            IAttackStrategy strategy, SkillConfig config = null) =>
            new SkillCaster(data, caster, targetProvider, strategy, config);
    }
}
