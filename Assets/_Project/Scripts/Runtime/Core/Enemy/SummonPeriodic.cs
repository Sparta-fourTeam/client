using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>일정 시간마다 지정한 몬스터를 count마리 소환해 달라고 요청한다 (츠치구모의 [무리 군주]). 소환 총량에 상한이 있다</summary>
    public sealed class SummonPeriodic : IPassive
    {
        // 소환체가 한 점에 겹치지 않도록 가로로 벌리는 간격
        private const float Spread = 0.4f;

        private readonly int _monsterId;
        private readonly int _count;
        private readonly float _interval;
        private readonly int _maxTotal;

        private float _timer;
        private int _summoned;

        public SummonPeriodic(int monsterId, int count, float interval, int maxTotal)
        {
            if (count <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), "소환 수는 1 이상이어야 합니다.");
            }

            if (interval <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(interval), "소환 주기는 0보다 커야 합니다.");
            }

            if (maxTotal <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxTotal), "소환 총량은 1 이상이어야 합니다.");
            }

            _monsterId = monsterId;
            _count = count;
            _interval = interval;
            _maxTotal = maxTotal;
        }

        public void OnTick(EnemyModel self, float deltaTime)
        {
            if (_summoned >= _maxTotal)
            {
                return;
            }

            _timer += deltaTime;
            if (_timer < _interval)
            {
                return;
            }

            _timer = 0f;
            int amount = Math.Min(_count, _maxTotal - _summoned);
            for (int i = 0; i < amount; i++)
            {
                float offset = (i - (amount - 1) / 2f) * Spread;
                self.RequestSpawn(new EnemySpawnRequest(_monsterId, self.Position + new Vector2(offset, 0f)));
            }

            _summoned += amount;
        }
    }
}
