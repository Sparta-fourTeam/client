using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public class SkillReactionEffectsTests
    {
        [TestCase("Explosion", 2f)]
        [TestCase("AdditionalLightning", 1f)]
        public void AuthoredReactionBursts_UseBillboardsAndFinishBeforeRootDestruction(string name, float scale)
        {
            var method = typeof(SkillReactionEffects).GetMethod("Spawn", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            var visual = (GameObject)method.Invoke(null, new object[] { name, new Vector2(3, 4), scale });
            try
            {
                Assert.IsNotNull(visual);
                Assert.AreEqual(new Vector3(3, 4, 0), visual.transform.position);
                Assert.AreEqual(Vector3.one * scale, visual.transform.localScale);
                var root = visual.GetComponent<ParticleSystem>();
                Assert.AreEqual(ParticleSystemStopAction.Destroy, root.main.stopAction);
                foreach (var effect in visual.GetComponentsInChildren<ParticleSystem>())
                {
                    Assert.IsFalse(effect.main.loop);
                    if (effect == root) { continue; }
                    Assert.LessOrEqual(effect.main.startLifetime.constantMax, root.main.duration);
                    var renderer = effect.GetComponent<ParticleSystemRenderer>();
                    Assert.AreEqual(ParticleSystemRenderMode.Billboard, renderer.renderMode);
                    Assert.IsNotNull(effect.textureSheetAnimation.GetSprite(0));
                    effect.Simulate(.05f, false, true, false);
                    Assert.Greater(effect.particleCount, 0, effect.name);
                }
            }
            finally { Object.DestroyImmediate(visual); }
        }
    }
}
