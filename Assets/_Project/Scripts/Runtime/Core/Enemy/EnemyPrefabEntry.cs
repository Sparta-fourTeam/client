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

        [Header("적 체력 및 속도")]
        public float Speed; // 해당 프리팹의 이동 속도
        public int MaxHp;

        [Header("적 공격 관련")]
        public AttackType AttackType;
        public int Damage;
        public float AttackInterval;
        public float AttackRange;

        public EnemyAttackStats CreateAttackStats() => new EnemyAttackStats(AttackType, Damage, AttackInterval, AttackRange);
    }
}
