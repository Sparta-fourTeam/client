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
        public bool TryHit(object target)
        {
            if (target == null || Exhausted || !hitTargets.Add(target)) return false;
            remainingHits--;
            return true;
        }
    }
}
