using System;

namespace Game.Core
{
    /// <summary>
    /// 주기적 가속: interval초마다 duration초 동안 이속이 multiplier배가 된다(독살무사의 "일시적으로 이동 속도 대폭 상승").
    /// 기절·빙결·마비 중에는 시간이 흐르지 않는다.
    /// </summary>
    public sealed class SpeedBoostPassive : IPassive
    {
        private readonly float _interval;
        private readonly float _duration;
        private readonly float _multiplier;
        private float _timer;

        public SpeedBoostPassive(float interval, float duration, float multiplier)
        {
            if (!(interval > 0f) || float.IsInfinity(interval))
            {
                throw new ArgumentOutOfRangeException(nameof(interval), "주기는 0보다 커야 합니다.");
            }

            if (!(duration > 0f) || float.IsInfinity(duration))
            {
                throw new ArgumentOutOfRangeException(nameof(duration), "지속시간은 0보다 커야 합니다.");
            }

            if (!(multiplier > 0f) || float.IsInfinity(multiplier))
            {
                throw new ArgumentOutOfRangeException(nameof(multiplier), "이속 배율은 0보다 커야 합니다.");
            }

            _interval = interval;
            _duration = duration;
            _multiplier = multiplier;
        }

        public void OnTick(EnemyModel self, float deltaTime)
        {
            _timer += deltaTime;
            if (_timer >= _interval)
            {
                _timer -= _interval;
                self.AddSpeedBoost(this, _multiplier, _duration);
            }
        }
    }
}
