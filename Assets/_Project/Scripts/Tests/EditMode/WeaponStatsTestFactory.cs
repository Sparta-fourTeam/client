using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    internal static class WeaponStatsTestFactory
    {
        public static WeaponStats Apply(WeaponStats stats, string type, float value)
        {
            Assert.IsTrue(WeaponStatEffects.TryApply(stats, new[] { new EffectDef { kind = type, value = value } }, out var result));
            return result;
        }
    }
}
