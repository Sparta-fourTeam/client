using Game.Core;
using Game.Core.Messages;
using MessagePipe;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public class EnemyStatusEffectsTests
    {
        private sealed class Publisher<T> : IPublisher<T> { public void Publish(T message) { } }
        private static EnemyModel Model() => new EnemyModel(1, Vector2.zero, 0, EnemyType.Normal, 10000,
            new EnemyAttackStats(AttackType.Melee, 1, 1, 0), new Publisher<EnemyHpChanged>(), new Publisher<EnemyDied>());
        private static void Sync(EnemyStatusEffects view) => typeof(EnemyStatusEffects).GetMethod("Sync",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(view, null);

        [Test]
        public void AuthoredStatuses_StartTogetherExpireStopOnDisableAndClearOnDeath()
        {
            var go = new GameObject("StatusVfxTest");
            try
            {
                var view = go.AddComponent<EnemyStatusEffects>(); var model = Model(); view.Bind(model);
                var effects = go.GetComponentsInChildren<ParticleSystem>(); Assert.AreEqual(7, effects.Length);
                foreach (var effect in effects)
                {
                    Assert.IsFalse(effect.isPlaying);
                    Assert.IsNotNull(effect.GetComponent<ParticleSystemRenderer>().sharedMaterial.GetTexture("_BaseMap"));
                }
                model.ApplyFreeze(1); model.ApplyBurn(1, 1); model.ApplyParalysis(1); model.ApplyStun(1);
                model.ApplySlow(.2f, 1); model.ApplyVulnerability(.2f, 1); Sync(view);
                foreach (var effect in effects) { Assert.AreEqual(effect.name != "Frostbite", effect.isPlaying, effect.name); }
                model.TickStatus(2); Sync(view);
                foreach (var effect in effects) { Assert.IsFalse(effect.isPlaying, effect.name); }
                model.ApplyFrostbite(1); Sync(view);
                Assert.IsTrue(go.transform.Find("EnemyStatuses(Clone)/Frostbite").GetComponent<ParticleSystem>().isPlaying);
                go.SetActive(false);
                foreach (var effect in effects) { Assert.IsFalse(effect.isPlaying, effect.name); }
                go.SetActive(true); Sync(view); model.TakeDamage(100000); Sync(view);
                foreach (var effect in effects) { Assert.IsFalse(effect.isPlaying, effect.name); }
                view.Bind(Model()); Sync(view);
                Assert.AreEqual(7, go.GetComponentsInChildren<ParticleSystem>().Length);
                foreach (var effect in effects) { Assert.IsFalse(effect.isPlaying, effect.name); }
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void JudgementArtwork_ReturnsToAnimatedBaseOnReuse()
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Skills/Lightning_Lv1.prefab");
            var go = Object.Instantiate(prefab);
            try
            {
                var effect = go.GetComponent<HitscanEffect>();
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                var upgraded = (GameObject)typeof(HitscanEffect).GetField("judgementVisual", flags).GetValue(effect);
                var regular = (GameObject)typeof(HitscanEffect).GetField("regularVisual", flags).GetValue(effect);
                Assert.IsNotNull(upgraded); Assert.IsNotNull(regular);
                effect.SetVisualForm(SkillForm.JudgementThunder);
                Assert.IsTrue(upgraded.activeSelf); Assert.IsFalse(regular.activeSelf);
                effect.SetVisualForm(SkillForm.Default);
                Assert.IsFalse(upgraded.activeSelf); Assert.IsTrue(regular.activeSelf);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
