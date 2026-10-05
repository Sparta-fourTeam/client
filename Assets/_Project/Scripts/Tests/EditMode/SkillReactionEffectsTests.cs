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

        [TestCase("Skills/Fireball", SkillForm.Default)]
        [TestCase("Skills/Lightning_Lv1", SkillForm.Default)]
        [TestCase("Skills/IceSpear", SkillForm.TriangleIce)]
        [TestCase("Skills/LightningOrb", SkillForm.Default)]
        public void SecondaryCastScale_HalvesRenderedParticleBounds(string path, SkillForm form)
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/" + path + ".prefab");
            var full = Object.Instantiate(prefab); var half = Object.Instantiate(prefab);
            try
            {
                half.transform.localScale *= .5f;
                foreach (var go in new[] { full, half })
                {
                    var projectile = go.GetComponent<Projectile>();
                    if (projectile != null) { projectile.SetVisualForm(form); }
                    foreach (var p in go.GetComponentsInChildren<ParticleSystem>())
                    {
                        p.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                        p.useAutoRandomSeed = false; p.randomSeed = 123;
                        p.Simulate(.1f, false, true, false); p.Pause(false);
                    }
                }
                var source = System.Array.Find(full.GetComponentsInChildren<ParticleSystem>(), p => p.particleCount > 0);
                Assert.IsNotNull(source, path);
                var match = System.Array.Find(half.GetComponentsInChildren<ParticleSystem>(), p => p.name == source.name);
                var fullBounds = source.GetComponent<ParticleSystemRenderer>().bounds.size;
                var halfBounds = match.GetComponent<ParticleSystemRenderer>().bounds.size;
                Assert.Greater(fullBounds.x + fullBounds.y, 0);
                Assert.AreEqual(fullBounds.x * .5f, halfBounds.x, .02f, path + " width");
                Assert.AreEqual(fullBounds.y * .5f, halfBounds.y, .02f, path + " height");
            }
            finally { Object.DestroyImmediate(full); Object.DestroyImmediate(half); }
        }
    }
}
