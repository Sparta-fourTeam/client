using System;

namespace Game.Core
{
    /// <summary>즉발 스킬의 시전·연발 타이밍. 시작 시 쿨타임을 걸고 연발 중 새 주기는 시작하지 않는다.</summary>
    public sealed class CastClock
    {
        public float RemainingCooldown { get; set; }
        private int remainingCasts;
        private float repeatTimer;

        public void Tick(float deltaTime, float cooldown, int castCount, float interval, Action fire) =>
            Tick(deltaTime, cooldown, castCount, interval, () => { fire(); return true; });

        /// <param name="fire">시전하고 실제로 쏘았는지 돌려준다. false(쏠 대상이 없음)이면 쿨타임을 쓰지 않고 준비 상태로 기다리다가
        /// 대상이 생기는 프레임에 바로 시전한다. 연발 중의 추가 시전은 결과를 보지 않는다</param>
        public void Tick(float deltaTime, float cooldown, int castCount, float interval, Func<bool> fire)
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

            RemainingCooldown = 0;
            if (!fire())
            {
                return;
            }

            RemainingCooldown = Math.Max(0f, cooldown);
            remainingCasts = Math.Max(1, castCount) - 1;
            repeatTimer = Math.Max(0.001f, interval);
        }
    }
}
