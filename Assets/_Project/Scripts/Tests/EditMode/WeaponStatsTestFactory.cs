using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    internal static class WeaponStatsTestFactory
    {
        public static WeaponStats Apply(WeaponStats stats, UpgradeType type, float value)
        {
            Assert.IsTrue(WeaponStatEffects.TryApply(stats, new[] { new StatEffect { type = type, value = value } }, out var result));
            return result;
        }
    }
}
