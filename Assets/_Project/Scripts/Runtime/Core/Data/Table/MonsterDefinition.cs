using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>몬스터 테이블 한 행. 체력·속도·공격 값과 등급(보스·엘리트), 공격 방식을 정한다. 프리팹은 클라이언트의 MonsterAssetTable이 ID로 찾는다</summary>
    [Serializable]
    public class MonsterDefinition
    {
        public int Id, Hp, Damage;
        public float Speed, AttackInterval;

        /// <summary>원거리 몬스터가 공격하는 거리. 근접은 0</summary>
        public float AttackRange;

        /// <summary>원거리 몬스터의 투사체 속도. 근접은 0</summary>
        public float ProjectileSpeed;

        public bool IsBoss;

        /// <summary>공격 한 번에 연달아 나가는 횟수. 1이면 연속 공격이 아니다(생략하면 1)</summary>
        public int BurstCount = 1;

        /// <summary>연속 공격에서 다음 타격까지의 간격(초). BurstCount가 2 이상일 때 필요하다</summary>
        public float BurstInterval;

        /// <summary>엘리트 등급. 웨이브가 정한 엘리트 수만큼 스테이지의 엘리트 몬스터 중에서 나온다</summary>
        public bool IsElite;

        /// <summary>속성별 받는 피해의 가감. 키는 Element 이름(Neutral, Fire, Ice, Wind, Lightning, Earth), 값은 약점 +, 저항 -, -1은 무효다.
        /// 예: {"Fire": 0.5}는 화속성 피해를 50% 더 받는다. 비우면 속성에 따른 변화가 없다</summary>
        public Dictionary<string, float> Resists = new Dictionary<string, float>();

        /// <summary>시전 형태별 받는 피해의 가감. 키는 CastType 이름(Projectile, Hitscan, Area, Beam, Chain). 예: {"Projectile": -0.7}은 투사체 피해를 70% 덜 받는다</summary>
        public Dictionary<string, float> CastResists = new Dictionary<string, float>();

        /// <summary>켜면 투사체의 직접 충격 피해를 무효로 하고 관통을 막는다. 폭발, 상태이상 같은 부가 반응은 그대로 걸린다</summary>
        public bool BlocksProjectile;

        /// <summary>이 몬스터가 가진 패시브. 없으면 비어 있다</summary>
        public List<PassiveDefinition> Passives = new List<PassiveDefinition>();

        /// <summary>등급. 보스, 엘리트, 그 외는 일반</summary>
        public EnemyType GetEnemyType() => IsBoss ? EnemyType.Boss : IsElite ? EnemyType.Elite : EnemyType.Normal;

        /// <summary>투사체 속도가 있으면 원거리, 없으면 근접</summary>
        public AttackType GetAttackType() => ProjectileSpeed > 0f ? AttackType.Ranged : AttackType.Melee;

        public void Validate()
        {
            DamageProfile.From(this);

            if (IsBoss && IsElite)
            {
                throw new InvalidOperationException($"Monsters {Id}: IsBoss와 IsElite를 함께 켤 수 없습니다");
            }

            if (Hp <= 0)
            {
                throw new InvalidOperationException($"Monsters {Id}: Hp는 1 이상이어야 합니다");
            }

            if (AttackInterval <= 0f)
            {
                throw new InvalidOperationException($"Monsters {Id}: AttackInterval은 0보다 커야 합니다");
            }

            if (BurstCount < 1)
            {
                throw new InvalidOperationException($"Monsters {Id}: BurstCount는 1 이상이어야 합니다");
            }

            if (BurstCount > 1 && (BurstInterval <= 0f || AttackInterval <= BurstInterval * (BurstCount - 1)))
            {
                throw new InvalidOperationException($"Monsters {Id}: BurstInterval은 0보다 크고 연속 공격이 AttackInterval 안에 끝나야 합니다");
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
