using System;
using System.Collections.Generic;

namespace Game.Core
{
    public sealed class ProjectileHitLedger
    {
        private readonly HashSet<object> hitTargets = new();
        private int remainingHits;
        public bool Exhausted => remainingHits <= 0;
        public void Reset(int pierceCount)
        {
            hitTargets.Clear();
            remainingHits = Math.Max(0, pierceCount) + 1;
        }
        /// <summary>남은 관통을 모두 없앤다. 관통을 막는 대상에 맞았을 때 쓴다</summary>
        public void Exhaust() => remainingHits = 0;

        public bool TryHit(object target)
        {
            if (target == null || Exhausted || !hitTargets.Add(target))
            {
                return false;
            }

            remainingHits--;
            return true;
        }
    }
}
