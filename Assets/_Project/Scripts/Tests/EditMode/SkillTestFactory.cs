using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    internal static class SkillTestFactory
    {
        public static SkillStats Apply(SkillStats stats, string type, float value)
        {
            Assert.IsTrue(SkillStatEffects.TryApply(stats, new[] { new EffectDef { kind = type, value = value } }, out var result));
            return result;
        }
    }
}
