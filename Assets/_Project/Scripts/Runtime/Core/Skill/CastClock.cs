using System;

namespace Game.Core
{
    /// <summary>즉발 스킬의 시전·연발 타이밍. 시작 시 쿨타임을 걸고 연발 중 새 주기는 시작하지 않는다.</summary>
    public sealed class CastClock
    {
        public float RemainingCooldown { get; set; }
        private int remainingCasts;
        private float repeatTimer;

        public void Tick(float deltaTime, float cooldown, int castCount, float interval, Action fire)
        {
            if (deltaTime < 0 || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            }

            RemainingCooldown -= deltaTime;
            if (remainingCasts > 0)
            {
                repeatTimer -= deltaTime;
                while (remainingCasts > 0 && repeatTimer <= 0)
                {
                    remainingCasts--;
                    repeatTimer += Math.Max(0.001f, interval);
                    fire();
                }
                return;
            }
            if (RemainingCooldown > 0)
            {
                return;
            }

            RemainingCooldown = Math.Max(0f, cooldown);
            remainingCasts = Math.Max(1, castCount) - 1;
            repeatTimer = Math.Max(0.001f, interval);
            fire();
        }
    }
}
