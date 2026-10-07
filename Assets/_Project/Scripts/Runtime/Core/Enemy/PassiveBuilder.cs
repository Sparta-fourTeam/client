using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>몬스터 행의 Passives 정의로 패시브 객체와 면역을 만든다. 상태가 있는 패시브라 호출마다 새로 만든다</summary>
    public static class PassiveBuilder
    {
        public static IReadOnlyList<IPassive> BuildPassives(MonsterDefinition monster)
        {
            if (monster.Passives == null || monster.Passives.Count == 0)
            {
                return null;
            }

            var passives = new List<IPassive>();
            foreach (var definition in monster.Passives)
            {
                switch (definition.Kind)
                {
                    case PassiveKind.Spawn:
                        passives.Add(new SpawnPassive(definition.ToSpawnTrigger(), definition.MonsterId, definition.Count,
                            definition.Interval, definition.MaxTotal));
                        break;
                    case PassiveKind.Immunity:
                        break; // 면역은 객체가 아니라 BuildImmunities가 플래그로 만든다
                    case PassiveKind.Modifier:
                        break; // 효과 배수는 객체가 아니라 BuildModifiers가 값으로 만든다
                    default:
                        throw new InvalidOperationException($"Monsters {monster.Id}: 알 수 없는 패시브 Kind입니다: {definition.Kind}");
                }
            }

            return passives.Count > 0 ? passives : null;
        }

        /// <summary>Modifier 패시브들을 효과 배수로 합친다. 같은 대상이 여러 개면 곱한다. 없으면 EffectModifiers.None</summary>
        public static EffectModifiers BuildModifiers(MonsterDefinition monster)
        {
            float knockback = 1f;
            float burnDuration = 1f;
            bool any = false;
            foreach (var definition in monster.Passives ?? new List<PassiveDefinition>())
            {
                if (definition.Kind != PassiveKind.Modifier)
                {
                    continue;
                }

                any = true;
                switch (definition.ToModifierTarget())
                {
                    case ModifierTarget.KnockbackDistance:
                        knockback *= definition.Multiplier.Value;
                        break;
                    case ModifierTarget.BurnDuration:
                        burnDuration *= definition.Multiplier.Value;
                        break;
                }
            }

            return any ? new EffectModifiers(knockback, burnDuration) : EffectModifiers.None;
        }

        public static StatusImmunity BuildImmunities(MonsterDefinition monster)
        {
            var flags = StatusImmunity.None;
            if (monster.Passives == null)
            {
                return flags;
            }

            foreach (var definition in monster.Passives)
            {
                if (definition.Kind == PassiveKind.Immunity)
                {
                    flags |= definition.ToImmunities();
                }
            }

            return flags;
        }
    }
}
