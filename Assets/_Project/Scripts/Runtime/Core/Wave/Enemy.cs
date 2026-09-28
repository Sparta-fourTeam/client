using UnityEngine;

namespace Game.Core.Wave
{
    // 몬스터의 종류
    public enum EnemyType
    {
        Normal,
        Elite,
        Boss
    }

    public class Enemy
    {
        private readonly float _speed; // 적 이동 속도
        private readonly EnemyType _type; // 적 타입

        public Vector2 Position { get; private set; }
        public EnemyType Type => _type;

        // 스폰 위치, 이동 속도, 타입 지정해서 몬스터 생성
        public Enemy(Vector2 spawnPosition, float speed, EnemyType type)
        {
            Position = spawnPosition;
            _speed = speed;
            _type = type;
        }
    }
}
