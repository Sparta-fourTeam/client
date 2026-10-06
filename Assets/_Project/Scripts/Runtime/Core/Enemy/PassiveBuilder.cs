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
                    case PassiveKind.SplitOnDeath:
                        passives.Add(new SplitOnDeath(definition.MonsterId, definition.Count));
                        break;
                    case PassiveKind.SummonPeriodic:
                        passives.Add(new SummonPeriodic(definition.MonsterId, definition.Count, definition.Interval, definition.MaxTotal));
                        break;
                    case PassiveKind.Immunity:
                        break; // 면역은 객체가 아니라 BuildImmunities가 플래그로 만든다
                    default:
                        throw new InvalidOperationException($"Monsters {monster.Id}: 알 수 없는 패시브 Kind입니다: {definition.Kind}");
                }
            }

            return passives.Count > 0 ? passives : null;
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
