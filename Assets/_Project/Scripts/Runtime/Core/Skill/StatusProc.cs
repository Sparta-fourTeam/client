using System;
using UnityEngine;

namespace Game.Core
{
    public static class StatusProc
    {
        public static bool Roll(float chance, Func<float> random = null)
        {
            if (float.IsNaN(chance) || float.IsInfinity(chance) || chance <= 0) return false;
            if (chance >= 1) return true;
            return (random ?? (() => UnityEngine.Random.value))() < chance;
        }
    }
}
