using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>몬스터 테이블 한 행. 프리팹(EnemyPrefabEntry)이 MonsterId로 이 행을 가리켜 체력·속도·공격 값을 받는다</summary>
    [Serializable]
    public class MonsterDefinition
    {
        public int Id, Exp, Hp, Damage, GaugeValue;
        public float Speed, AttackInterval;

        /// <summary>원거리 몬스터가 공격하는 거리. 근접은 0</summary>
        public float AttackRange;

        /// <summary>원거리 몬스터의 투사체 속도. 근접은 0</summary>
        public float ProjectileSpeed;

        public bool IsBoss;

        /// <summary>이 몬스터가 가진 패시브. 없으면 비어 있다</summary>
        public List<PassiveDefinition> Passives = new List<PassiveDefinition>();

        public void Validate()
        {
            if (Hp <= 0)
            {
                throw new InvalidOperationException($"Monsters {Id}: Hp는 1 이상이어야 합니다");
            }

            if (AttackInterval <= 0f)
            {
                throw new InvalidOperationException($"Monsters {Id}: AttackInterval은 0보다 커야 합니다");
            }

            if (Damage < 0 || AttackRange < 0f || Speed < 0f || ProjectileSpeed < 0f)
            {
                throw new InvalidOperationException($"Monsters {Id}: Damage, Speed, AttackRange, ProjectileSpeed는 음수일 수 없습니다");
            }

            foreach (var passive in Passives ?? new List<PassiveDefinition>())
            {
                if (passive == null)
                {
                    throw new InvalidOperationException($"Monsters {Id}: Passives에 null 항목이 있습니다");
                }

                passive.Validate(Id);
            }
        }
    }
}
