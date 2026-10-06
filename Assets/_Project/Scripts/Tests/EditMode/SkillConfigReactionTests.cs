using System.Collections.Generic;
using System.Reflection;
using Game.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Pool;

namespace Game.Tests
{
    public sealed class SkillConfigReactionTests
    {
        private static SkillConfig Config() => SkillConfig.FromDefinition(new SkillData
        {
            castType = CastType.Projectile,
            baseStats = new SkillBaseStats { cast = { baseDamage = 100, range = 8 }, projectile = { speed = 10 } }
        });

        private sealed class Target : IEnemyTarget, IFreezableTarget
        {
            public Vector2 Position => Vector2.one;
            public int Damage;
            public float Freeze;
            public void TakeDamage(int damage) => Damage += damage;
            public void ApplyFreeze(float duration) => Freeze = duration;
        }

        private static void Tick(Projectile projectile, float deltaTime) =>
            typeof(Projectile).GetMethod("Tick", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(projectile, new object[] { deltaTime });

        [Test]
        public void Builder_ComposesStatsCastsTransformAndHitReactions()
        {
            var builder = new SkillConfigBuilder(Config());
            int childCasts = 0;
            Assert.IsTrue(builder.TryApply(new IUpgradeEffect[]
            {
                new StatUpgradeEffect("damage", 60),
                new CastUpgradeEffect("castCount", 1),
                new ReactionUpgradeEffect(AttackEvent.Hit, new CastSkillReaction(_ => childCasts += 2)),
                new TransformUpgradeEffect(SkillForm.FireLog)
            }));
            var config = builder.Build();
            Assert.AreEqual(160, config.Stats.Cast.Damage, .001f);
            Assert.AreEqual(2, config.Stats.Cast.Count);
            Assert.AreEqual(SkillForm.FireLog, config.Stats.Cast.Form);
            Assert.AreEqual(8, config.Attack.Range);
            config.Reactions.Raise(AttackEvent.Start, new AttackContext(Vector2.zero, Vector3.up));
            Assert.AreEqual(0, childCasts);
            config.Reactions.Raise(AttackEvent.Hit, new AttackContext(Vector2.one, Vector3.up));
            Assert.AreEqual(2, childCasts);
        }

        [Test]
        public void Builder_FailedBatchRollsBackValuesAndReactions()
        {
            var builder = new SkillConfigBuilder(Config());
            int children = 0;
            Assert.IsFalse(builder.TryApply(new IUpgradeEffect[]
            {
                new StatUpgradeEffect("damage", 60),
                new ReactionUpgradeEffect(AttackEvent.Hit, new CastSkillReaction(_ => children++)),
                new CastUpgradeEffect("damage", 60)
            }));
            var result = builder.Build();
            Assert.AreEqual(100, result.Stats.Cast.Damage);
            result.Reactions.Raise(AttackEvent.Hit, new AttackContext(Vector2.zero, Vector3.up));
            Assert.AreEqual(0, children);
        }

        [Test]
        public void Builder_LaterUpgradeDoesNotChangePreviousConfig()
        {
            var builder = new SkillConfigBuilder(Config());
            var previous = builder.Build();
            Assert.IsTrue(builder.TryApply(new[] { new StatUpgradeEffect("damage", 60) }));
            Assert.AreEqual(100, previous.Stats.Cast.Damage);
            Assert.AreEqual(160, builder.Build().Stats.Cast.Damage, .001f);
        }

        [Test]
        public void Reactions_PreserveDamageStatusChildOrderAndSeparateTriggers()
        {
            var target = new Target();
            var events = new List<string>();
            var program = new AttackReactions(new[]
            {
                new ReactionBinding(AttackEvent.Hit, new DamageReaction(60)),
                new ReactionBinding(AttackEvent.Hit, new StatusReaction<IFreezableTarget>(.5f, (t, _) => t.ApplyFreeze(2))),
                new ReactionBinding(AttackEvent.Hit, new CastSkillReaction(c =>
                {
                    Assert.AreSame(target, c.Target);
                    Assert.AreEqual(60, target.Damage);
                    Assert.AreEqual(2, target.Freeze);
                    events.Add("hit");
                })),
                new ReactionBinding(AttackEvent.Kill, new CastSkillReaction(_ => events.Add("kill"))),
                new ReactionBinding(AttackEvent.Expired, new CastSkillReaction(_ => events.Add("expired")))
            });
            var context = new AttackContext(Vector2.one, Vector3.up, target, () => 0);
            program.Raise(AttackEvent.Hit, context);
            CollectionAssert.AreEqual(new[] { "hit" }, events);
            program.Raise(AttackEvent.Kill, context);
            program.Raise(AttackEvent.Expired, context);
            CollectionAssert.AreEqual(new[] { "hit", "kill", "expired" }, events);
        }

        [Test]
        public void Projectile_ExpiredCastsChildOnceAndPoolReuseDropsOldReactions()
        {
            int children = 0, starts = 0;
            var reactions = new AttackReactions(new[]
            {
                new ReactionBinding(AttackEvent.Start, new CastSkillReaction(_ => starts++)),
                new ReactionBinding(AttackEvent.Expired, new CastSkillReaction(c =>
                {
                    Assert.AreEqual(new Vector2(0, 2), c.Position);
                    children += 2;
                }))
            });
            var pool = new ObjectPool<Projectile>(
                () => new GameObject("ReactionLifecycleTest").AddComponent<Projectile>(),
                p => p.gameObject.SetActive(true), p => p.gameObject.SetActive(false),
                p => UnityEngine.Object.DestroyImmediate(p.gameObject));
            try
            {
                var projectile = pool.Get();
                projectile.Init(pool, new ProjectileSpawnSettings
                {
                    Direction = Vector3.up,
                    Speed = 10,
                    Lifetime = .1f,
                    Reactions = reactions
                });
                Tick(projectile, .2f);
                Tick(projectile, .2f);
                Assert.AreEqual(1, starts);
                Assert.AreEqual(2, children);
                var reused = pool.Get();
                Assert.AreSame(projectile, reused);
                reused.Init(pool, new ProjectileSpawnSettings { Lifetime = .1f });
                Tick(reused, .2f);
                Assert.AreEqual(1, starts);
                Assert.AreEqual(2, children);
            }
            finally { pool.Clear(); }
        }

        [Test]
        public void HitReactions_KeepValuesFromBuildTime()
        {
            float damage = 60, freeze = 2;
            var reactions = new HitReactionBuilder().Damage(damage).Freeze(freeze).Build();
            damage = 999;
            freeze = 99;
            var target = new Target();
            reactions.Raise(AttackEvent.Hit, new AttackContext(target.Position, Vector3.up, target));
            Assert.AreEqual(60, target.Damage);
            Assert.AreEqual(2, target.Freeze);
        }

        [Test]
        public void PeriodicReaction_CatchesUpWithoutSharingTimersBetweenAttacks()
        {
            int calls = 0;
            var periodic = new PeriodicReaction(1, new CastSkillReaction(_ => calls++));
            periodic.Execute(new AttackContext(Vector2.zero, Vector3.up, deltaTime: .5f, elapsed: .5f));
            Assert.AreEqual(0, calls);
            periodic.Execute(new AttackContext(Vector2.zero, Vector3.up, deltaTime: 2, elapsed: 2.5f));
            Assert.AreEqual(2, calls);
            // A second attack starts from its own elapsed time, using the same immutable program.
            periodic.Execute(new AttackContext(Vector2.one, Vector3.up, deltaTime: 1, elapsed: 1));
            Assert.AreEqual(3, calls);
        }

        [Test]
        public void ChildCast_UsesCountAndOneChanceRollPerEvent()
        {
            int calls = 0, rolls = 0;
            var reaction = new CastSkillReaction(_ => calls++, count: 2, chance: .05f);
            reaction.Execute(new AttackContext(Vector2.zero, Vector3.up, randomValue: () => { rolls++; return .04f; }));
            Assert.AreEqual(2, calls);
            Assert.AreEqual(1, rolls);
            reaction.Execute(new AttackContext(Vector2.zero, Vector3.up, randomValue: () => .06f));
            Assert.AreEqual(2, calls);
        }

        [Test]
        public void Projectile_FinalTickRunsScheduledReactionBeforeExpired()
        {
            var events = new List<string>();
            var program = new AttackReactions(new[]
            {
                new ReactionBinding(AttackEvent.Tick, new PeriodicReaction(1, new CastSkillReaction(_ => events.Add("tick")))),
                new ReactionBinding(AttackEvent.Expired, new CastSkillReaction(_ => events.Add("expired")))
            });
            var pool = new ObjectPool<Projectile>(() => new GameObject("FinalTickTest").AddComponent<Projectile>(),
                actionOnDestroy: p => UnityEngine.Object.DestroyImmediate(p.gameObject));
            try
            {
                var projectile = pool.Get();
                projectile.Init(pool, new ProjectileSpawnSettings { Lifetime = 1, Reactions = program });
                Tick(projectile, 2);
                CollectionAssert.AreEqual(new[] { "tick", "expired" }, events);
            }
            finally { pool.Clear(); }
        }

        [Test]
        public void Factory_UsesCompiledConfigAndUpgradePreservesItsReactions()
        {
            int children = 0;
            var data = new SkillData
            {
                maxLevel = 15,
                baseStats = new SkillBaseStats { cast = { baseDamage = 100, range = 8 } }
            };
            var builder = new SkillConfigBuilder(SkillConfig.FromDefinition(data));
            Assert.IsTrue(builder.TryApply(new IUpgradeEffect[]
            {
                new ReactionUpgradeEffect(AttackEvent.Expired, new CastSkillReaction(_ => children++))
            }));
            var config = builder.Build();
            var prefab = new GameObject("CompiledConfigFactoryTest");
            prefab.AddComponent<Projectile>();
            try
            {
                var weapon = SkillFactory.Create(data, prefab, prefab.transform, new NullEnemyTargetProvider(), config: config);
                Assert.AreSame(config, weapon.Config);
                Assert.IsTrue(weapon.LevelUp(new SkillUpgradeOption
                {
                    id = "damage",
                    effects = new List<EffectDef> { new EffectDef { kind = "damage", value = 20 } }
                }));
                Assert.AreEqual(120, weapon.Stats.Cast.Damage, .001f);
                weapon.Config.Reactions.Raise(AttackEvent.Expired, new AttackContext(Vector2.zero, Vector3.up));
                Assert.AreEqual(1, children);
                Assert.AreEqual(100, config.Stats.Cast.Damage);
            }
            finally { UnityEngine.Object.DestroyImmediate(prefab); }
        }
    }
}
