using System;

namespace Game.Core.Defense
{
    public sealed class WallAttack
    {
        private readonly float _interval;
        private float _elapsedTime;

        public WallAttack(float interval)
        {
            if (interval <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(interval));
            }

            _interval = interval;
        }

        public int Tick(float deltaTime)
        {
            _elapsedTime += deltaTime;
            int hits = 0;
            while (_elapsedTime >= _interval)
            {
                _elapsedTime -= _interval;
                hits++;
            }
            return hits;
        }
    }
}
