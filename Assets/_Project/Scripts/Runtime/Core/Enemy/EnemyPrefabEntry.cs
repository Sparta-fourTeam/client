using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    [Serializable]
    public struct EnemyPrefabEntry
    {
        [Header("타입 및 연결")]
        public EnemyType Type;
        public Enemy Prefab;
        public EnemyProjectile ProjectilePrefab; // 원거리만

        [Header("스탯")]
        [Tooltip("Monsters 테이블의 행 ID. 체력, 속도, 공격 값은 테이블에서 온다")]
        public int MonsterId;

        [Header("공격 방식")]
        public AttackType AttackType;

        [Header("분열 (슬라임)")]
        [Tooltip("죽을 때 만들 몬스터의 Monsters ID. 0이면 분열하지 않는다")]
        public int SplitMonsterId;
        public int SplitCount;

        [Header("면역 (오니)")]
        [Tooltip("걸리지 않는 상태이상")]
        public StatusImmunity Immunities;

        [Header("생성 방식")]
        [Tooltip("켜면 웨이브 스폰의 무작위 선택에서 빼고, 분열·소환 요청으로만 만든다")]
        public bool SpawnOnly;

        public IReadOnlyList<IPassive> CreatePassives() =>
            SplitMonsterId > 0 && SplitCount > 0
                ? new IPassive[] { new SplitOnDeath(SplitMonsterId, SplitCount) }
                : null;

        public EnemyAttackStats CreateAttackStats(MonsterDefinition monster) =>
            new EnemyAttackStats(AttackType, monster.Damage, monster.AttackInterval, monster.AttackRange, monster.ProjectileSpeed);
    }
}
