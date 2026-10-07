using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 지정한 몬스터를 count마리 만들어 달라고 요청한다.
    /// OnDeath는 죽을 때 한 번(슬라임의 [불안정]), Interval은 interval초마다 총 maxTotal마리까지(츠치구모의 [무리 군주]).
    /// </summary>
    public sealed class SpawnPassive : IPassive
    {
        // 만들어진 몬스터가 한 점에 겹치지 않도록 가로로 벌리는 간격
        private const float Spread = 0.4f;

        private readonly SpawnTrigger _trigger;
        private readonly int _monsterId;
        private readonly int _count;
        private readonly float _interval;
        private readonly int _maxTotal;

        private float _timer;
        private int _spawned;

        public SpawnPassive(SpawnTrigger trigger, int monsterId, int count, float interval = 0f, int maxTotal = 0)
        {
            if (count <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), "생성 수는 1 이상이어야 합니다.");
            }

            if (trigger == SpawnTrigger.Interval)
            {
                if (interval <= 0f)
                {
                    throw new ArgumentOutOfRangeException(nameof(interval), "생성 주기는 0보다 커야 합니다.");
                }

                if (maxTotal <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(maxTotal), "생성 총량은 1 이상이어야 합니다.");
                }
            }

            _trigger = trigger;
            _monsterId = monsterId;
            _count = count;
            _interval = interval;
            _maxTotal = maxTotal;
        }

        public void OnDied(EnemyModel self)
        {
            if (_trigger == SpawnTrigger.OnDeath)
            {
                Request(self, _count);
            }
        }

        public void OnTick(EnemyModel self, float deltaTime)
        {
            if (_trigger != SpawnTrigger.Interval || _spawned >= _maxTotal)
            {
                return;
            }

            _timer += deltaTime;
            if (_timer < _interval)
            {
                return;
            }

            _timer = 0f;
            int amount = Math.Min(_count, _maxTotal - _spawned);
            Request(self, amount);
            _spawned += amount;
        }

        private void Request(EnemyModel self, int amount)
        {
            for (int i = 0; i < amount; i++)
            {
                float offset = (i - (amount - 1) / 2f) * Spread;
                self.RequestSpawn(new EnemySpawnRequest(_monsterId, self.Position + new Vector2(offset, 0f)));
            }
        }
    }
}
