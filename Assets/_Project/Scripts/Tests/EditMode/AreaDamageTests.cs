using System.Collections.Generic;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public class AreaDamageTests
    {
        private class Target : IEnemyTarget
        {
            public Vector2 Position { get; set; }
            public int Damage;
            public void TakeDamage(int value) => Damage += value;
        }
        private class Provider : IEnemyTargetProvider
        {
            public readonly List<IEnemyTarget> Targets = new();
            public int GetNearest(Vector2 from, int count, List<IEnemyTarget> results)
            { results.Clear(); results.AddRange(Targets); return results.Count; }
        }
        [Test]
        public void HitscanExplosion_FiresOnceAndClearsOnPoolReuse()
        {
            var go = new GameObject("HitscanExplosionTest");
            try
            {
                var effect = go.AddComponent<HitscanEffect>();
                var pool = new UnityEngine.Pool.ObjectPool<HitscanEffect>(() => effect);
                var hit = typeof(HitscanEffect).GetMethod("Hit", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                int count = 0;
                pool.Get().Init(pool, Vector3.zero, 10,
                    reactions: new HitReactionBuilder().On(AttackEvent.Impact, new CastSkillReaction(_ => count++)).Build());
                hit.Invoke(effect, null);
                hit.Invoke(effect, null);
                Assert.AreEqual(1, count);
                pool.Release(effect);
                pool.Get().Init(pool, Vector3.zero, 10);
                hit.Invoke(effect, null);
                Assert.AreEqual(1, count);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void JudgementCard_ChangesFormAndPooledVisualScaleResets()
        {
            var go = new GameObject("JudgementScaleTest"); go.AddComponent<HitscanEffect>();
            go.transform.position = new Vector3(1000, 1000, 0);
            try
            {
                var data = new DefaultSkillDataProvider(new GameDataStore()).LoadAll().Find(w => w.id == 3);
                var provider = new Provider(); provider.Targets.Add(new Target { Position = new Vector2(1000, 1000) });
                var weapon = Casters.Hitscan(data, go, go.transform, provider);
                Assert.IsTrue(weapon.LevelUp(data.upgrades.Find(c => c.id == "lightning_judgement")));
                Assert.IsFalse(weapon.LevelUp(data.upgrades.Find(c => c.id == "lightning_judgement")));
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                var statsField = typeof(SkillBase).GetField("stats", flags);
                Assert.AreEqual(96, ((SkillStats)statsField.GetValue(weapon)).Cast.Damage);
                var fire = typeof(SkillCaster).GetMethod("OnFire", flags);
                fire.Invoke(weapon, null);
                HitscanEffect clone = null;
                foreach (var effect in Object.FindObjectsByType<HitscanEffect>(FindObjectsSortMode.None))
                { if (effect.name == "JudgementScaleTest(Clone)") { clone = effect; } }
                Assert.AreEqual(Vector3.one * 1.5f, clone.transform.localScale);
                typeof(HitscanEffect).GetMethod("Release", flags).Invoke(clone, null);
                statsField.SetValue(weapon, SkillStats.FromDefinition(data.baseStats));
                fire.Invoke(weapon, null);
                Assert.AreEqual(Vector3.one, clone.transform.localScale);
            }
            finally
            {
                foreach (var effect in Object.FindObjectsByType<HitscanEffect>(FindObjectsSortMode.None))
                { if (effect.name == "JudgementScaleTest(Clone)") { Object.DestroyImmediate(effect.gameObject); } }
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Explosion_HitsBoundaryAndDeduplicatesTargets()
        {
            var provider = new Provider();
            var inside = new Target();
            var boundary = new Target { Position = Vector2.right };
            var outside = new Target { Position = Vector2.right * 1.01f };
            provider.Targets.AddRange(new IEnemyTarget[] { inside, inside, boundary, outside });
            Assert.AreEqual(2, AreaDamage.Apply(provider, Vector2.zero, 1, 10));
            Assert.AreEqual(10, inside.Damage);
            Assert.AreEqual(10, boundary.Damage);
            Assert.AreEqual(0, outside.Damage);
            Assert.AreEqual(0, AreaDamage.Apply(provider, Vector2.zero, float.NaN, 10));
        }
    }
}
