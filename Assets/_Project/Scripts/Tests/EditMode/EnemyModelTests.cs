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

        private EnemyModel CreateEnemy(int id = 1, int maxHp = 10, float speed = 0f, Vector2 position = default)
        {
            return new EnemyModel(id, position, speed, EnemyType.Normal, maxHp, TestAttack, _hpChanged, _died);
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

            Assert.AreEqual(5, enemy.FrostbiteStacks);
            enemy.TickStatus(0);
            Assert.AreEqual(1000, enemy.Hp);
            enemy.TickStatus(0.5f);
            Assert.AreEqual(1000, enemy.Hp);
            enemy.TickStatus(0.5f);
            Assert.AreEqual(990, enemy.Hp);
            enemy.TickStatus(20);
            Assert.AreEqual(900, enemy.Hp);
            Assert.AreEqual(0, enemy.FrostbiteStacks);
        }

        [Test]
        public void Frostbite_DeathPublishesOnceAndRejectsInvalidDamage()
        {
            var enemy = CreateEnemy();
            enemy.ApplyFrostbite(float.NaN);
            enemy.ApplyFrostbite(-1);
            Assert.AreEqual(0, enemy.FrostbiteStacks);
            enemy.ApplyFrostbite(20);
            enemy.TickStatus(10);
            enemy.TickStatus(10);
            Assert.AreEqual(1, _died.Published.Count);
            Assert.AreEqual(0, enemy.FrostbiteStacks);
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
            Assert.AreEqual(1, enemy.ParalysisRemaining);
            enemy.ApplyFreeze(2);
            enemy.TickStatus(1);
            Assert.IsFalse(enemy.IsParalyzed);
            Assert.IsTrue(enemy.IsFrozen);
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
            Assert.AreEqual(100, enemy.Hp);
            enemy.TickStatus(0.5f);
            Assert.AreEqual(98, enemy.Hp);
            enemy.TickStatus(20);
            Assert.AreEqual(88, enemy.Hp);
            Assert.AreEqual(0, enemy.BurnRemaining);
        }

        [Test]
        public void Burn_CoexistsWithFreezeAndFrostbiteAndPublishesDeathOnce()
        {
            var enemy = CreateEnemy(maxHp: 10);
            enemy.ApplyBurn(float.NaN, 6);
            Assert.AreEqual(0, enemy.BurnRemaining);
            enemy.ApplyBurn(3, 6);
            enemy.ApplyFreeze(2);
            enemy.ApplyFrostbite(2);
            enemy.TickStatus(1);
            Assert.AreEqual(5, enemy.Hp);
            Assert.IsTrue(enemy.IsFrozen);
            enemy.TickStatus(20);
            enemy.TickStatus(20);
            Assert.AreEqual(1, _died.Published.Count);
            Assert.AreEqual(0, enemy.BurnRemaining);
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
            Assert.AreEqual(1, enemy.FreezeRemaining);
            enemy.TickStatus(0);
            Assert.IsTrue(enemy.IsFrozen);
            enemy.TickStatus(1);
            Assert.IsFalse(enemy.IsFrozen);
            enemy.Move(0.5f);
            Assert.AreEqual(4.5f, enemy.Position.y);
        }

        [Test]
        public void Freeze_StopsAttackAndRejectsInvalidOrDeadTarget()
        {
            var enemy = CreateEnemy();
            int attacks = 0;
            enemy.Attacked += () => attacks++;
            enemy.ApplyFreeze(2);
            // 빙결 중에는 벽/투사체를 접근하거나 공격 타이머를 진행하지 않는다.
            enemy.Attack(1, null, null);
            Assert.AreEqual(0, attacks);
            enemy.ApplyFreeze(float.NaN);
            enemy.ApplyFreeze(float.PositiveInfinity);
            enemy.ApplyFreeze(-1);
            Assert.AreEqual(2, enemy.FreezeRemaining);
            enemy.TickStatus(2);
            enemy.TakeDamage(10);
            enemy.ApplyFreeze(2);
            Assert.IsFalse(enemy.IsFrozen);
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
            Assert.AreEqual(10, enemy.MaxHp);
            Assert.AreEqual(10, enemy.Hp);
            Assert.IsFalse(enemy.IsDead);
            Assert.AreEqual(0, _hpChanged.Published.Count); // 생성만으로는 발행 안 함
        }

        [Test]
        public void Constructor_KeepsAttackType()
        {
            var ranged = new EnemyAttackStats(AttackType.Ranged, 5, 2f, 4f);
            var enemy = new EnemyModel(1, Vector2.zero, 0f, EnemyType.Normal, 10, ranged, _hpChanged, _died);

            Assert.AreEqual(AttackType.Ranged, enemy.AttackType);
        }

        // ───────── 체력 감소 ─────────

        [Test]
        public void TakeDamage_ReducesHp_AndPublishesHpChanged()
        {
            var enemy = CreateEnemy(id: 7, maxHp: 10);

            enemy.TakeDamage(3);

            Assert.AreEqual(7, enemy.Hp);
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

            Assert.AreEqual(5, enemy.Hp);
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

            Assert.AreEqual(10, enemy.Hp);
            Assert.AreEqual(0, _hpChanged.Published.Count);
            Assert.AreEqual(0, _died.Published.Count);
        }

        // ───────── 사망 ─────────

        [Test]
        public void TakeDamage_ExactlyToZero_Dies_AndPublishesEnemyDied()
        {
            var enemy = CreateEnemy(id: 5, maxHp: 10);

            enemy.TakeDamage(10);

            Assert.AreEqual(0, enemy.Hp);
            Assert.IsTrue(enemy.IsDead);
            Assert.AreEqual(1, _died.Published.Count);
            Assert.AreEqual(5, _died.Published[0].EnemyId);
        }

        [Test]
        public void TakeDamage_Overkill_ClampsToZero()
        {
            var enemy = CreateEnemy(maxHp: 10);

            enemy.TakeDamage(999);

            Assert.AreEqual(0, enemy.Hp);
            Assert.AreEqual(0, _hpChanged.Published[0].Current); // 음수로 안 나감
        }

        [Test]
        public void TakeDamage_PublishesHpChangedBeforeEnemyDied()
        {
            var order = new List<string>();
            var hp = new OrderRecorder<EnemyHpChanged>(order, "hp");
            var died = new OrderRecorder<EnemyDied>(order, "died");
            var enemy = new EnemyModel(1, Vector2.zero, 0f, EnemyType.Normal, 10, TestAttack, hp, died);

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

            Assert.AreEqual(0, enemy.Hp);
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

            Assert.AreEqual(6, enemy.Hp);
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
