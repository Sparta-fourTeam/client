using UnityEngine;

namespace Game.Core
{
    // 적 종류
    public enum EnemyType
    {
        Normal,
        Elite,
        Boss
    }

    public class Enemy
    {
        // 아래쪽 방향으로 이동.
        private static readonly Vector2 _moveDirection = Vector2.down;
        private readonly float _speed; // 적 이동 속도
        private readonly EnemyType _type; // 적 타입

        public Vector2 Position { get; private set; }
        public EnemyType Type => _type;
        public float Speed => _speed;

        // 스폰 위치, 이동 속도, 타입 지정해서 몬스터 생성
        public Enemy(Vector2 spawnPosition, float speed, EnemyType type)
        {
            Position = spawnPosition;
            _speed = speed;
            _type = type;
        }

        // 매 프레임 speed만큼 이동
        public void Move(float deltaTime)
        {
            Position += _moveDirection * (_speed * deltaTime);
        }
    }
}
