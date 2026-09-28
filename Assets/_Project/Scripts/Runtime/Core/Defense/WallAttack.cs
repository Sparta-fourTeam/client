using System;

namespace Game.Core.Defense
{
    public sealed class WallAttack
    {
        private readonly IWall _wall;
        private readonly int _damage;
        private readonly float _interval;
        private float _elapsedTime;

        public WallAttack(IWall wall, int damage, float interval)
        {
            if (interval <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(interval));
            }
            _wall = wall;
            _damage = damage;
            _interval = interval;
        }

        public void Tick(float deltaTime)
        {
            if (_wall.IsDestroyed)
            {
                return;
            }

            _elapsedTime += deltaTime;

            while (_elapsedTime >= _interval && !_wall.IsDestroyed)
            {
                _elapsedTime -= _interval;
                _wall.TakeDamage(_damage);
            }
        }
    }
}
