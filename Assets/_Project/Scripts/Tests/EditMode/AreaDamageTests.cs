using System.Collections.Generic;
using Game.Core;
using Game.Core.Messages;
using MessagePipe;
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

        private sealed class Publisher<T> : IPublisher<T>
        {
            public void Publish(T message) { }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Hitscan_DenseWaveDeduplicatesCollidersAndResetsBuffersOnReuse(bool includeTriggers)
        {
            bool previousTriggers = Physics2D.queriesHitTriggers;
            var go = new GameObject("DenseHitscanTest");
            var enemies = new List<Enemy>();
            Enemy trigger = null;
            var origin = new Vector3(1000, 1000, 0);
            var hp = new Publisher<EnemyHpChanged>();
            var died = new Publisher<EnemyDied>();
            try
            {
                Physics2D.queriesHitTriggers = includeTriggers;
                // More colliders than a typical fixed buffer; every enemy must be hit exactly once.
                for (int i = 0; i < 130; i++)
                {
                    var enemy = TestEnemy.Create(new EnemyModel(i, 0, EnemyType.Normal, 100,
                        new EnemyAttackStats(AttackType.Melee, 0, 1, 0), hp, died), origin);
                    enemy.transform.position += Vector3.forward * (i % 3 - 1);
                    enemy.gameObject.layer = 31;
                    enemy.gameObject.AddComponent<CircleCollider2D>();
                    enemy.gameObject.AddComponent<BoxCollider2D>();
                    enemies.Add(enemy);
                }
                trigger = TestEnemy.Create(new EnemyModel(131, 0, EnemyType.Normal, 100,
                    new EnemyAttackStats(AttackType.Melee, 0, 1, 0), hp, died), origin);
                trigger.gameObject.layer = 31;
                trigger.gameObject.AddComponent<CircleCollider2D>().isTrigger = true;
                var effect = go.AddComponent<HitscanEffect>();
                typeof(HitscanEffect).GetField("targetMask", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .SetValue(effect, (LayerMask)(1 << 31));
                var hit = typeof(HitscanEffect).GetMethod("Hit", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                using var pool = new UnityEngine.Pool.ObjectPool<HitscanEffect>(() => effect);
                var hitDepths = new List<float>();
                var reactions = new HitReactionBuilder().On(AttackEvent.Hit,
                    new CastSkillReaction(context => hitDepths.Add(((Enemy)context.Target).transform.position.z))).Build();
                Physics2D.SyncTransforms();
                for (int cast = 1; cast <= 2; cast++)
                {
                    hitDepths.Clear();
                    pool.Get().Init(pool, origin, 10, reactions: reactions, impactRadius: 2);
                    hit.Invoke(effect, null);
                    foreach (var enemy in enemies) { Assert.That(enemy.CurrentHp, Is.EqualTo(100 - 10 * cast)); }
                    Assert.That(trigger.CurrentHp, Is.EqualTo(includeTriggers ? 100 - 10 * cast : 100));
                    Assert.That(hitDepths, Is.Ordered, "범위 타격 반응은 기존 OverlapCircleAll의 Z 오름차순을 보존한다");
                    pool.Release(effect);
                }
            }
            finally
            {
                Physics2D.queriesHitTriggers = previousTriggers;
                foreach (var enemy in enemies)
                {
                    if (enemy != null) { Object.DestroyImmediate(enemy.gameObject); }
                }
                if (trigger != null) { Object.DestroyImmediate(trigger.gameObject); }
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
