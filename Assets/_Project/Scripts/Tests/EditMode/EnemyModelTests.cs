using System;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Messages;
using MessagePipe;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public class EnemyModelTests
    {
        private class FakePublisher<T> : IPublisher<T>
        {
            public readonly List<T> Published = new();
            public void Publish(T message) => Published.Add(message);
        }

        private static readonly EnemyAttackStats TestAttack = new EnemyAttackStats(AttackType.Melee, 10, 1f, 0f);

        private FakePublisher<EnemyHpChanged> _hpChanged;
        private FakePublisher<EnemyDied> _died;

        [SetUp]
        public void SetUp()
        {
            _hpChanged = new FakePublisher<EnemyHpChanged>();
            _died = new FakePublisher<EnemyDied>();
        }

        // 위치(Transform)는 Enemy가, 규칙(HP, 상태이상)은 enemy.Model이 갖는다
        private Enemy CreateEnemy(int id = 1, int maxHp = 10, float speed = 0f, Vector2 position = default)
        {
            return TestEnemy.Create(new EnemyModel(id, speed, EnemyType.Normal, maxHp, TestAttack, _hpChanged, _died), position);
        }

        [Test]
        public void AreaSlow_StrongestSourceWinsAndRemovalRestoresMovement()
        {
            var enemy = CreateEnemy(speed: 10, position: Vector2.up * 100);
            var weak = new object(); var strong = new object();
            enemy.SetAreaSlow(weak, .3f); enemy.SetAreaSlow(strong, .6f);
            Assert.AreEqual(.4f, enemy.Model.MovementMultiplier, .001f);
            enemy.Move(1); Assert.AreEqual(96, enemy.Position.y, .001f);
            enemy.RemoveAreaSlow(strong); enemy.Move(1);
            Assert.AreEqual(89, enemy.Position.y, .001f);
            enemy.RemoveAreaSlow(weak); Assert.AreEqual(1, enemy.Model.MovementMultiplier);
            enemy.SetAreaSlow(weak, float.NaN); enemy.SetAreaSlow(weak, 1); enemy.SetAreaSlow(null, .3f);
            Assert.AreEqual(1, enemy.Model.MovementMultiplier);
            enemy.SetAreaSlow(weak, .3f); enemy.TakeDamage(100);
            Assert.AreEqual(1, enemy.Model.MovementMultiplier);
        }

        [Test]
        public void Stun_StopsMovementAndAttackIndependentlyOfParalysisAndExpires()
        {
            var enemy = CreateEnemy(speed: 1, position: Vector2.up * 100);
            enemy.ApplyStun(1);
            Assert.DoesNotThrow(() => enemy.Attack(1, null, null));
            enemy.ApplyParalysis(2);
            enemy.Move(1); Assert.AreEqual(100, enemy.Position.y);
            enemy.ApplyStun(.5f); Assert.AreEqual(1, enemy.Model.StunRemaining);
            enemy.TickStatus(1); Assert.IsFalse(enemy.Model.IsStunned); Assert.IsTrue(enemy.Model.IsParalyzed);
            enemy.TickStatus(1); enemy.Move(1); Assert.AreEqual(99, enemy.Position.y);
            enemy.ApplyStun(float.NaN); enemy.ApplyStun(-1); Assert.AreEqual(0, enemy.Model.StunRemaining);
        }

        [Test]
        public void TimedSlow_DoesNotStackWithAreaSlowAndRestoresStrongestRemainingSource()
        {
            var enemy = CreateEnemy(speed: 10, position: Vector2.up * 100);
            var source = new object(); enemy.SetAreaSlow(source, .6f); enemy.ApplySlow(.3f, 2);
            Assert.AreEqual(.4f, enemy.Model.MovementMultiplier, .001f);
            enemy.RemoveAreaSlow(source); Assert.AreEqual(.7f, enemy.Model.MovementMultiplier, .001f);
            enemy.ApplySlow(.2f, 1); Assert.AreEqual(2, enemy.Model.SlowRemaining);
            enemy.Move(1); Assert.AreEqual(93, enemy.Position.y, .001f);
            enemy.TickStatus(2); Assert.AreEqual(1, enemy.Model.MovementMultiplier);
            enemy.ApplySlow(float.NaN, 1); enemy.ApplySlow(1, 2); Assert.AreEqual(0, enemy.Model.SlowRemaining);
        }

        [Test]
        public void Vulnerability_AmplifiesAllReceivedDamageWithoutStackingAndExpires()
        {
            var enemy = CreateEnemy(maxHp: 1000);
            enemy.TakeDamage(10); Assert.AreEqual(990, enemy.Model.Hp);
            enemy.ApplyVulnerability(.2f, 6); enemy.ApplyVulnerability(.2f, 6);
            enemy.TakeDamage(10); Assert.AreEqual(978, enemy.Model.Hp);
            enemy.ApplyBurn(10, 1); enemy.ApplyFrostbite(10);
            enemy.TickStatus(1); Assert.AreEqual(954, enemy.Model.Hp);
            Assert.AreEqual(5, enemy.Model.VulnerabilityRemaining);
            enemy.TickStatus(5);
            enemy.TakeDamage(10); Assert.AreEqual(884, enemy.Model.Hp);
            Assert.AreEqual(0, enemy.Model.VulnerabilityRatio);
            enemy.ApplyVulnerability(float.NaN, 1); Assert.AreEqual(0, enemy.Model.VulnerabilityRemaining);
            enemy.ApplyVulnerability(.2f, 1); enemy.TakeDamage(1000);
            Assert.IsTrue(enemy.IsDead); Assert.AreEqual(0, enemy.Model.VulnerabilityRemaining);
        }

        [Test]
        public void Vulnerability_LargeStatusTickOnlyAmplifiesDamageBeforeExpiry()
        {
            var enemy = CreateEnemy(maxHp: 100);
            enemy.ApplyBurn(10, 3); enemy.ApplyVulnerability(.2f, 1);
            enemy.TickStatus(3);
            Assert.AreEqual(68, enemy.Model.Hp); Assert.AreEqual(0, enemy.Model.VulnerabilityRemaining);
        }

        private class LightningProvider : IEnemyTargetProvider
        {
            public readonly List<IEnemyTarget> Targets = new();
            public int GetNearest(Vector2 from, int count, List<IEnemyTarget> results)
            { results.Clear(); results.AddRange(Targets); return results.Count; }
        }

        [Test]
        public void LightningKill_SpawnsOneTargetedSecondaryWithoutInheritedEffectsOrRecursion()
        {
            var prefab = new GameObject("KillLightningTest");
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            try
            {
                var position = new Vector2(1000, 1000);
                prefab.transform.position = position;
                var victim = CreateEnemy(maxHp: 10, position: position);
                var neighbour = CreateEnemy(2, 24, position: position + Vector2.right * 2);
                var farther = CreateEnemy(3, 100, position: position + Vector2.right * 3);
                var dead = CreateEnemy(4, 1, position: position + Vector2.right); dead.TakeDamage(1);
                victim.gameObject.AddComponent<CircleCollider2D>(); victim.gameObject.AddComponent<BoxCollider2D>();
                var template = prefab.AddComponent<HitscanEffect>();
                typeof(HitscanEffect).GetField("radius", flags).SetValue(template, .5f);
                typeof(HitscanEffect).GetField("targetMask", flags).SetValue(template, (LayerMask)(-1));
                var provider = new LightningProvider(); provider.Targets.AddRange(new IEnemyTarget[] { victim, farther, dead, neighbour });
                var data = new DefaultSkillDataProvider(new GameDataStore()).LoadAll().Find(w => w.id == 3);
                var weapon = Casters.Hitscan(data, prefab, prefab.transform, provider);
                Assert.IsTrue(weapon.LevelUp(data.upgrades.Find(c => c.id == "lightning_sanction")));
                Assert.IsFalse(weapon.LevelUp(data.upgrades.Find(c => c.id == "lightning_sanction")));
                typeof(SkillCaster).GetMethod("OnFire", flags).Invoke(weapon, null);
                HitscanEffect main = null;
                foreach (var effect in UnityEngine.Object.FindObjectsByType<HitscanEffect>(FindObjectsSortMode.None))
                { if (effect.name == "KillLightningTest(Clone)") { main = effect; } }
                // Upgrades after casting must not change this kill callback's damage snapshot.
                weapon.LevelUp(data.upgrades.Find(c => c.id == "lightning_voltage"));
                Physics2D.SyncTransforms();
                var hit = typeof(HitscanEffect).GetMethod("Hit", flags);
                hit.Invoke(main, null); hit.Invoke(main, null);
                Assert.IsTrue(victim.IsDead);
                HitscanEffect secondary = null; int count = 0;
                foreach (var effect in UnityEngine.Object.FindObjectsByType<HitscanEffect>(FindObjectsSortMode.None))
                { if (effect.name == "KillLightningTest(Clone)") { count++; if (effect != main) { secondary = effect; } } }
                Assert.AreEqual(2, count);
                Assert.AreEqual(40f, (float)typeof(HitscanEffect).GetField("damage", flags).GetValue(secondary));
                Assert.AreEqual(Vector3.one * .5f, secondary.transform.localScale);
                hit.Invoke(secondary, null); hit.Invoke(secondary, null);
                Assert.IsTrue(neighbour.IsDead); Assert.AreEqual(100, farther.Model.Hp);
                Assert.AreEqual(0, neighbour.Model.ParalysisRemaining);
                Assert.AreSame(AttackReactions.Empty, typeof(HitscanEffect).GetField("reactions", flags).GetValue(secondary));
            }
            finally
            {
                foreach (var effect in UnityEngine.Object.FindObjectsByType<HitscanEffect>(FindObjectsSortMode.None))
                { if (effect.name == "KillLightningTest(Clone)") { UnityEngine.Object.DestroyImmediate(effect.gameObject); } }
                UnityEngine.Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void SecondaryLightning_SkipsDeadTargetAndClearsTargetAndKillCallbackOnReuse()
        {
            var go = new GameObject("SecondaryReuseTest");
            try
            {
                var target = CreateEnemy(maxHp: 1); target.TakeDamage(1);
                var effect = go.AddComponent<HitscanEffect>();
                var pool = new UnityEngine.Pool.ObjectPool<HitscanEffect>(() => effect);
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                var hit = typeof(HitscanEffect).GetMethod("Hit", flags);
                int kills = 0;
                pool.Get().Init(pool, Vector3.zero, 10, directTarget: target,
                    reactions: new HitReactionBuilder().On(AttackEvent.Kill, new CastSkillReaction(_ => kills++)).Build());
                hit.Invoke(effect, null);
                Assert.AreEqual(1, _died.Published.Count); Assert.AreEqual(0, kills);
                pool.Release(effect); pool.Get().Init(pool, Vector3.zero, 1);
                Assert.IsNull(typeof(HitscanEffect).GetField("directTarget", flags).GetValue(effect));
                Assert.AreSame(AttackReactions.Empty, typeof(HitscanEffect).GetField("reactions", flags).GetValue(effect));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void KillLightning_NoSurvivingTargetWithinRangeCreatesNoEffect()
        {
            var go = new GameObject("NoKillTargetTest"); go.AddComponent<HitscanEffect>();
            try
            {
                var data = new DefaultSkillDataProvider(new GameDataStore()).LoadAll().Find(w => w.id == 3);
                var provider = new LightningProvider();
                provider.Targets.Add(CreateEnemy(position: Vector2.right * (data.baseStats.cast.range + 1)));
                var weapon = Casters.Hitscan(data, go, go.transform, provider);
                weapon.LevelUp(data.upgrades.Find(c => c.id == "lightning_sanction"));
                var pool = (UnityEngine.Pool.ObjectPool<HitscanEffect>)Casters.Hitscan(weapon).Pool;
                var callback = new HitscanCastEffects(weapon.Stats, data.baseStats.cast.range, provider, pool, null, Vector3.one)
                    .CreateKillLightningCallback();
                callback(Vector2.zero);
                foreach (var effect in UnityEngine.Object.FindObjectsByType<HitscanEffect>(FindObjectsSortMode.None))
                { Assert.AreNotEqual("NoKillTargetTest(Clone)", effect.name); }
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void Knockback_NormalizesDirectionWorksDuringFreezeAndRejectsInvalidValues()
        {
            var enemy = CreateEnemy(position: new Vector2(0, 5));
            enemy.ApplyFreeze(2);
            enemy.ApplyKnockback(new Vector2(0, 10), 0.6f);
            Assert.AreEqual(5.6f, enemy.Position.y, 0.001f);
            enemy.ApplyKnockback(Vector2.up, float.NaN);
            enemy.ApplyKnockback(Vector2.up, -1);
            enemy.ApplyKnockback(new Vector2(float.PositiveInfinity, 0), 1);
            Assert.AreEqual(5.6f, enemy.Position.y, 0.001f);
            enemy.TakeDamage(10);
            enemy.ApplyKnockback(Vector2.up, 1);
            Assert.AreEqual(5.6f, enemy.Position.y, 0.001f);
        }

        [Test]
        public void Frostbite_StacksToFiveTicksEachSecondAndExpiresAtTenSeconds()
        {
            var enemy = CreateEnemy(maxHp: 1000);
            for (int i = 0; i < 6; i++)
            {
                enemy.ApplyFrostbite(2);
            }

            Assert.AreEqual(5, enemy.Model.FrostbiteStacks);
            enemy.TickStatus(0);
            Assert.AreEqual(1000, enemy.Model.Hp);
            enemy.TickStatus(0.5f);
            Assert.AreEqual(1000, enemy.Model.Hp);
            enemy.TickStatus(0.5f);
            Assert.AreEqual(990, enemy.Model.Hp);
            enemy.TickStatus(20);
            Assert.AreEqual(900, enemy.Model.Hp);
            Assert.AreEqual(0, enemy.Model.FrostbiteStacks);
        }

        [Test]
        public void Frostbite_DeathPublishesOnceAndRejectsInvalidDamage()
        {
            var enemy = CreateEnemy();
            enemy.ApplyFrostbite(float.NaN);
            enemy.ApplyFrostbite(-1);
            Assert.AreEqual(0, enemy.Model.FrostbiteStacks);
            enemy.ApplyFrostbite(20);
            enemy.TickStatus(10);
            enemy.TickStatus(10);
            Assert.AreEqual(1, _died.Published.Count);
            Assert.AreEqual(0, enemy.Model.FrostbiteStacks);
        }

        // ───────── 생성 ─────────

        [Test]
        public void Paralysis_StopsMovementAndAttackExpiresAndRemainsIndependentOfFreeze()
        {
            var enemy = CreateEnemy(speed: 1, position: Vector2.up * 5);
            enemy.ApplyParalysis(1);
            enemy.ApplyParalysis(0.1f);
            enemy.Move(1);
            enemy.Attack(1, null, null);
            Assert.AreEqual(5, enemy.Position.y);
            enemy.TickStatus(0);
            Assert.AreEqual(1, enemy.Model.ParalysisRemaining);
            enemy.ApplyFreeze(2);
            enemy.TickStatus(1);
            Assert.IsFalse(enemy.Model.IsParalyzed);
            Assert.IsTrue(enemy.Model.IsFrozen);
            enemy.TickStatus(1);
            enemy.Move(1);
            Assert.AreEqual(4, enemy.Position.y);
        }

        [Test]
        public void Burn_RefreshDoesNotResetTickProgressOrStackAndExpires()
        {
            var enemy = CreateEnemy(maxHp: 100);
            enemy.ApplyBurn(2, 6);
            enemy.TickStatus(0.5f);
            enemy.ApplyBurn(1, 6);
            enemy.TickStatus(0);
            Assert.AreEqual(100, enemy.Model.Hp);
            enemy.TickStatus(0.5f);
            Assert.AreEqual(98, enemy.Model.Hp);
            enemy.TickStatus(20);
            Assert.AreEqual(88, enemy.Model.Hp);
            Assert.AreEqual(0, enemy.Model.BurnRemaining);
        }

        [Test]
        public void Burn_CoexistsWithFreezeAndFrostbiteAndPublishesDeathOnce()
        {
            var enemy = CreateEnemy(maxHp: 10);
            enemy.ApplyBurn(float.NaN, 6);
            Assert.AreEqual(0, enemy.Model.BurnRemaining);
            enemy.ApplyBurn(3, 6);
            enemy.ApplyFreeze(2);
            enemy.ApplyFrostbite(2);
            enemy.TickStatus(1);
            Assert.AreEqual(5, enemy.Model.Hp);
            Assert.IsTrue(enemy.Model.IsFrozen);
            enemy.TickStatus(20);
            enemy.TickStatus(20);
            Assert.AreEqual(1, _died.Published.Count);
            Assert.AreEqual(0, enemy.Model.BurnRemaining);
        }

        [Test]
        public void Inferno_AddsMaxHpDamagePerBurnTick()
        {
            var enemy = CreateEnemy(maxHp: 100);
            enemy.ApplyBurn(2, 6, 0.03f);
            enemy.TickStatus(1);
            Assert.AreEqual(95, enemy.Model.Hp);
            enemy.TickStatus(5);
            Assert.AreEqual(70, enemy.Model.Hp);
        }

        [Test]
        public void BurnDeathExplosion_TriggersOnceIncludingLastTickAndNotAfterExpiry()
        {
            var enemy = CreateEnemy(maxHp: 6);
            int explosions = 0;
            enemy.ApplyBurn(1, 6, onDeath: position => { explosions++; enemy.TakeDamage(10); });
            enemy.TickStatus(6);
            enemy.TakeDamage(10);
            Assert.AreEqual(1, explosions);
            Assert.AreEqual(1, _died.Published.Count);
            var expired = CreateEnemy(id: 2, maxHp: 100);
            expired.ApplyBurn(1, 6, onDeath: position => explosions++);
            expired.TickStatus(6);
            expired.TakeDamage(100);
            Assert.AreEqual(1, explosions);
        }

        private sealed class BurnTargets : IEnemyTargetProvider
        {
            public readonly List<IEnemyTarget> Targets = new();
            public int GetNearest(Vector2 from, int count, List<IEnemyTarget> results)
            { results.Clear(); results.AddRange(Targets); return results.Count; }
        }

        [Test]
        public void BurnDeathExplosion_ChainKillsEachEnemyOnce()
        {
            var provider = new BurnTargets();
            var enemies = new[] { CreateEnemy(1), CreateEnemy(2), CreateEnemy(3) };
            int explosions = 0;
            foreach (var enemy in enemies)
            {
                provider.Targets.Add(enemy);
                enemy.ApplyBurn(1, 6, onDeath: position => { explosions++; AreaDamage.Apply(provider, position, 1, 10); });
            }
            enemies[0].TakeDamage(10);
            Assert.AreEqual(3, explosions);
            Assert.AreEqual(3, _died.Published.Count);
            foreach (var enemy in enemies)
            {
                Assert.IsTrue(enemy.IsDead);
            }
        }

        [Test]
        public void PiercingProjectile_SkipsEnemyKilledByEarlierExplosion()
        {
            var go = new GameObject("PiercingExplosionTest");
            try
            {
                var provider = new BurnTargets();
                var first = CreateEnemy(1, position: Vector2.right * 0.5f);
                var second = CreateEnemy(2, position: Vector2.right * 0.7f);
                provider.Targets.Add(first); provider.Targets.Add(second);
                var projectile = go.AddComponent<Projectile>();
                var pool = new UnityEngine.Pool.ObjectPool<Projectile>(() => projectile);
                int impacts = 0;
                pool.Get().Init(pool, Vector3.zero, Vector3.right, 10, 10, 3, provider, pierceCount: 2,
                    onHit: (position, direction, target) => { impacts++; AreaDamage.Apply(provider, position, 1, 10); });
                typeof(Projectile).GetMethod("Tick", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .Invoke(projectile, new object[] { 0.1f });
                Assert.AreEqual(1, impacts);
                Assert.IsTrue(second.IsDead);
                Assert.AreEqual(2, _died.Published.Count);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void Hitscan_ParalyzesActualEnemyOnceAndClearsCallbackOnReuse()
        {
            var effectGo = new GameObject("HitscanStatusEffect");
            try
            {
                var position = new Vector3(1000, 1000, 0);
                var model = CreateEnemy(maxHp: 100, position: position);
                model.gameObject.AddComponent<CircleCollider2D>();
                model.gameObject.AddComponent<BoxCollider2D>();
                var effect = effectGo.AddComponent<HitscanEffect>();
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                typeof(HitscanEffect).GetField("radius", flags).SetValue(effect, 1f);
                typeof(HitscanEffect).GetField("targetMask", flags).SetValue(effect, (LayerMask)(-1));
                var hit = typeof(HitscanEffect).GetMethod("Hit", flags);
                var pool = new UnityEngine.Pool.ObjectPool<HitscanEffect>(() => effect);
                int procs = 0;
                pool.Get().Init(pool, position, 1, reactions: new HitReactionBuilder()
                    .On(AttackEvent.Hit, new StatusReaction<IParalyzableTarget>(1, (t, _) => { procs++; t.ApplyParalysis(2.5f); })).Build());
                Physics2D.SyncTransforms();
                hit.Invoke(effect, null); hit.Invoke(effect, null);
                Assert.AreEqual(99, model.Model.Hp);
                Assert.AreEqual(1, procs);
                Assert.AreEqual(2.5f, model.Model.ParalysisRemaining);
                model.TickStatus(1);
                pool.Release(effect);
                pool.Get().Init(pool, position, 1);
                hit.Invoke(effect, null);
                Assert.AreEqual(98, model.Model.Hp);
                Assert.AreEqual(1, procs);
                Assert.AreEqual(1.5f, model.Model.ParalysisRemaining);
            }
            finally { UnityEngine.Object.DestroyImmediate(effectGo); }
        }

        [Test]
        public void Freeze_StopsMovementRefreshesWithoutStackingAndExpires()
        {
            var enemy = CreateEnemy(speed: 1, position: new Vector2(0, 5));
            enemy.ApplyFreeze(2);
            enemy.Move(1);
            Assert.AreEqual(5, enemy.Position.y);
            enemy.TickStatus(1);
            enemy.ApplyFreeze(0.5f);
            Assert.AreEqual(1, enemy.Model.FreezeRemaining);
            enemy.TickStatus(0);
            Assert.IsTrue(enemy.Model.IsFrozen);
            enemy.TickStatus(1);
            Assert.IsFalse(enemy.Model.IsFrozen);
            enemy.Move(0.5f);
            Assert.AreEqual(4.5f, enemy.Position.y);
        }

        [Test]
        public void Freeze_StopsAttackAndRejectsInvalidOrDeadTarget()
        {
            var enemy = CreateEnemy();
            int attacks = 0;
            enemy.Model.Attacked += () => attacks++;
            enemy.ApplyFreeze(2);
            // 빙결 중에는 벽/투사체를 접근하거나 공격 타이머를 진행하지 않는다.
            enemy.Attack(1, null, null);
            Assert.AreEqual(0, attacks);
            enemy.ApplyFreeze(float.NaN);
            enemy.ApplyFreeze(float.PositiveInfinity);
            enemy.ApplyFreeze(-1);
            Assert.AreEqual(2, enemy.Model.FreezeRemaining);
            enemy.TickStatus(2);
            enemy.TakeDamage(10);
            enemy.ApplyFreeze(2);
            Assert.IsFalse(enemy.Model.IsFrozen);
        }

        [TestCase(0)]
        [TestCase(-5)]
        public void Constructor_NonPositiveMaxHp_Throws(int maxHp)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateEnemy(maxHp: maxHp));
        }

        [Test]
        public void Constructor_StartsWithFullHp()
        {
            var enemy = CreateEnemy(id: 3, maxHp: 10);

            Assert.AreEqual(3, enemy.Id);
            Assert.AreEqual(10, enemy.Model.MaxHp);
            Assert.AreEqual(10, enemy.Model.Hp);
            Assert.IsFalse(enemy.IsDead);
            Assert.AreEqual(0, _hpChanged.Published.Count); // 생성만으로는 발행 안 함
        }

        [Test]
        public void Constructor_KeepsAttackType()
        {
            var ranged = new EnemyAttackStats(AttackType.Ranged, 5, 2f, 4f, 8f);
            var enemy = new EnemyModel(1, 0f, EnemyType.Normal, 10, ranged, _hpChanged, _died);

            Assert.AreEqual(AttackType.Ranged, enemy.AttackType);
        }

        // ───────── 체력 감소 ─────────

        [Test]
        public void TakeDamage_ReducesHp_AndPublishesHpChanged()
        {
            var enemy = CreateEnemy(id: 7, maxHp: 10);

            enemy.TakeDamage(3);

            Assert.AreEqual(7, enemy.Model.Hp);
            Assert.AreEqual(1, _hpChanged.Published.Count);
            Assert.AreEqual(7, _hpChanged.Published[0].EnemyId);
            Assert.AreEqual(7, _hpChanged.Published[0].Current);
            Assert.AreEqual(10, _hpChanged.Published[0].Max);
            Assert.AreEqual(0, _died.Published.Count);
        }

        [Test]
        public void TakeDamage_MultipleHits_PublishesEachChange()
        {
            var enemy = CreateEnemy(maxHp: 10);

            enemy.TakeDamage(2);
            enemy.TakeDamage(3);

            Assert.AreEqual(5, enemy.Model.Hp);
            Assert.AreEqual(2, _hpChanged.Published.Count);
            Assert.AreEqual(8, _hpChanged.Published[0].Current);
            Assert.AreEqual(5, _hpChanged.Published[1].Current);
        }

        [TestCase(0)]
        [TestCase(-3)]
        public void TakeDamage_NonPositive_Ignored(int amount)
        {
            var enemy = CreateEnemy(maxHp: 10);

            enemy.TakeDamage(amount);

            Assert.AreEqual(10, enemy.Model.Hp);
            Assert.AreEqual(0, _hpChanged.Published.Count);
            Assert.AreEqual(0, _died.Published.Count);
        }

        // ───────── 사망 ─────────

        [Test]
        public void TakeDamage_ExactlyToZero_Dies_AndPublishesEnemyDied()
        {
            var enemy = CreateEnemy(id: 5, maxHp: 10);

            enemy.TakeDamage(10);

            Assert.AreEqual(0, enemy.Model.Hp);
            Assert.IsTrue(enemy.IsDead);
            Assert.AreEqual(1, _died.Published.Count);
            Assert.AreEqual(5, _died.Published[0].EnemyId);
        }

        [Test]
        public void TakeDamage_Overkill_ClampsToZero()
        {
            var enemy = CreateEnemy(maxHp: 10);

            enemy.TakeDamage(999);

            Assert.AreEqual(0, enemy.Model.Hp);
            Assert.AreEqual(0, _hpChanged.Published[0].Current); // 음수로 안 나감
        }

        [Test]
        public void TakeDamage_PublishesHpChangedBeforeEnemyDied()
        {
            var order = new List<string>();
            var hp = new OrderRecorder<EnemyHpChanged>(order, "hp");
            var died = new OrderRecorder<EnemyDied>(order, "died");
            var enemy = new EnemyModel(1, 0f, EnemyType.Normal, 10, TestAttack, hp, died);

            enemy.TakeDamage(10);

            CollectionAssert.AreEqual(new[] { "hp", "died" }, order); // Wall과 같은 순서
        }

        [Test]
        public void TakeDamage_AfterDeath_IgnoredAndNoMorePublishes()
        {
            var enemy = CreateEnemy(maxHp: 10);
            enemy.TakeDamage(10);

            enemy.TakeDamage(5);
            enemy.TakeDamage(5);

            Assert.AreEqual(0, enemy.Model.Hp);
            Assert.AreEqual(1, _hpChanged.Published.Count);
            Assert.AreEqual(1, _died.Published.Count); // 사망은 한 번만
        }

        // ───────── 이동 ─────────

        [Test]
        public void Move_AfterDeath_DoesNotMove()
        {
            var enemy = CreateEnemy(maxHp: 10, speed: 3f, position: new Vector2(0f, 5f));
            enemy.TakeDamage(10);

            enemy.Move(1f);

            Assert.AreEqual(5f, enemy.Position.y, 0.0001f);
        }

        [Test]
        public void Move_Alive_MovesDown()
        {
            var enemy = CreateEnemy(maxHp: 10, speed: 3f, position: new Vector2(0f, 5f));

            enemy.Move(1f);

            Assert.AreEqual(2f, enemy.Position.y, 0.0001f);
        }

        [Test]
        public void Move_DoesNotChangeX()
        {
            var enemy = CreateEnemy(maxHp: 10, speed: 3f, position: new Vector2(4f, 5f));

            enemy.Move(1f);

            Assert.AreEqual(4f, enemy.Position.x, 0.0001f); // 벽 쪽(아래)으로 일직선 이동
        }

        // ───────── IEnemyTarget ─────────

        [Test]
        public void AsEnemyTarget_TakeDamageWorksThroughInterface()
        {
            var enemy = CreateEnemy(maxHp: 10);
            IEnemyTarget target = enemy; // 스킬이 쓰는 방식

            target.TakeDamage(4);

            Assert.AreEqual(6, enemy.Model.Hp);
        }

        // 두 Publisher의 발행 순서를 하나의 목록에 기록
        private class OrderRecorder<T> : IPublisher<T>
        {
            private readonly List<string> _order;
            private readonly string _name;

            public OrderRecorder(List<string> order, string name)
            {
                _order = order;
                _name = name;
            }

            public void Publish(T message) => _order.Add(_name);
        }
    }
}
