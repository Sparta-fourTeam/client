using System;

namespace Game.Core
{
    public sealed class ReactiveCastClock
    {
        private double cooldown, repeat;
        private int pending;
        public void Tick(float deltaTime, bool trigger, int castCount, float cooldownSeconds, float interval, Action fire)
        {
            if (deltaTime < 0 || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) { throw new ArgumentOutOfRangeException(nameof(deltaTime)); }
            if (deltaTime == 0) { return; }
            cooldown = Math.Max(0, cooldown - deltaTime);
            if (pending > 0)
            {
                repeat -= deltaTime;
                while (pending > 0 && repeat <= 0.0000001)
                {
                    pending--; repeat += Math.Max(.001f, interval); fire();
                }
                return;
            }
            if (!trigger || cooldown > 0 || castCount <= 0) { return; }
            cooldown = cooldownSeconds; pending = castCount - 1; repeat = Math.Max(.001f, interval);
            fire();
        }
    }
}
