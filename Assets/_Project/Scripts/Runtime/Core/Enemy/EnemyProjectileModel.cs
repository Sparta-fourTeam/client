using Game.Core.Defense;
using UnityEngine;

namespace Game.Core
{
    public class EnemyProjectileModel
    {
        private static readonly Vector2 _direction = Vector2.down;
        private readonly float _speed;
        private readonly int _damage;

        public Vector2 Position { get; private set;  }
        public bool IsDone { get; private set;  }

        public EnemyProjectileModel(Vector2 from, float speed, int damage)
        {
            Position = from;
            _speed = speed;
            _damage = damage;
        }

        public void Tick(float deltaTime, Wall wall)
        {
            if(IsDone) return;
            Position += _direction * (_speed * deltaTime);

            if(Position.y <= wall.AttackLineY)
            {
                wall.TakeDamage(_damage);
                IsDone = true;
            }
        }
    }
}
