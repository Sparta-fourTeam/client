using System;
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

        public EnemyAttackStats CreateAttackStats(MonsterDefinition monster) =>
            new EnemyAttackStats(AttackType, monster.Damage, monster.AttackInterval, monster.AttackRange, monster.ProjectileSpeed);
    }
}
