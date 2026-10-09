using System;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Messages;
using MessagePipe;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    // 상태이상 면역 종류(점화·마비·빙결), 밀치기 저항, 점화 지속시간 배수와 그 데이터 정의
    public sealed class StatusModifierTests
    {
        private static readonly EnemyAttackStats Attack = new EnemyAttackStats(AttackType.Melee, 1, 1f, 0f);

        private sealed class Recorder<T> : IPublisher<T>
        {
            public void Publish(T message) { }
        }

        private static EnemyModel Model(StatusImmunity immunities = StatusImmunity.None, EffectModifiers modifiers = null) =>
            new EnemyModel(1, 0f, EnemyType.Normal, 100, Attack, new Recorder<EnemyHpChanged>(), new Recorder<EnemyDied>(),
                immunities: immunities, modifiers: modifiers);

        private static MonsterDefinition Monster(params PassiveDefinition[] passives) =>
            new MonsterDefinition { Id = 7, Hp = 10, AttackInterval = 1f, Passives = new List<PassiveDefinition>(passives) };

        private static PassiveDefinition Immune(params string[] statuses) =>
            new PassiveDefinition { Kind = PassiveKind.Immunity, Statuses = statuses };

        private static PassiveDefinition Modifier(string target, float? multiplier) =>
            new PassiveDefinition { Kind = PassiveKind.Modifier, Target = target, Multiplier = multiplier };

        // ───────── 면역 종류 ─────────

        [Test(Description = "점화 면역이면 점화에 걸리지 않는다")]
        public void BurnImmune_IgnoresBurn()
        {
            var model = Model(StatusImmunity.Burn);

            model.ApplyBurn(5f, 3f);
            model.TickStatus(1f);

            Assert.AreEqual(0f, model.BurnRemaining);
            Assert.AreEqual(100, model.Hp);
        }

        [Test]
        public void ParalysisImmune_IgnoresParalysis()
        {
            var model = Model(StatusImmunity.Paralysis);

            model.ApplyParalysis(3f);

            Assert.IsFalse(model.IsParalyzed);
        }

        [Test]
        public void FreezeImmune_IgnoresFreeze()
        {
            var model = Model(StatusImmunity.Freeze);

            model.ApplyFreeze(3f);

            Assert.IsFalse(model.IsFrozen);
        }

        [Test(Description = "면역은 지정한 상태이상만 막는다")]
        public void Immunity_OnlyBlocksListedStatuses()
        {
            var model = Model(StatusImmunity.Burn | StatusImmunity.Freeze);

            model.ApplyBurn(5f, 3f);
            model.ApplyFreeze(3f);
            model.ApplyParalysis(3f);
            model.ApplyStun(3f);

            Assert.AreEqual(0f, model.BurnRemaining);
            Assert.IsFalse(model.IsFrozen);
            Assert.IsTrue(model.IsParalyzed);
            Assert.IsTrue(model.IsStunned);
        }

        // ───────── 면역 데이터 ─────────

        [Test]
        public void ImmunityData_ParsesNewStatusNamesCaseInsensitively()
        {
            var monster = Monster(Immune("Burn", "paralysis", "FREEZE"));

            Assert.DoesNotThrow(() => monster.Validate());
            Assert.AreEqual(StatusImmunity.Burn | StatusImmunity.Paralysis | StatusImmunity.Freeze, PassiveBuilder.BuildImmunities(monster));
        }

        [Test(Description = "이름이 아닌 값(숫자, 빈 문자열)은 거부한다")]
        public void ImmunityData_RejectsNonNames()
        {
            Assert.Throws<InvalidOperationException>(() => Monster(Immune("3")).Validate());
            Assert.Throws<InvalidOperationException>(() => Monster(Immune("")).Validate());
            Assert.Throws<InvalidOperationException>(() => Monster(Immune("None")).Validate());
            Assert.Throws<InvalidOperationException>(() => Monster(Immune("Teleport")).Validate());
        }

        // ───────── Modifier 데이터 ─────────

        [Test]
        public void ModifierData_AcceptsKnownTargets()
        {
            Assert.DoesNotThrow(() => Monster(Modifier("KnockbackDistance", 0.5f), Modifier("burnduration", 4f)).Validate());
            Assert.DoesNotThrow(() => Monster(Modifier("KnockbackDistance", 0f)).Validate(), "0은 무효");
        }

        [Test]
        public void ModifierData_RejectsBadValues()
        {
            Assert.Throws<InvalidOperationException>(() => Monster(Modifier("Speed", 0.5f)).Validate(), "알 수 없는 Target");
            Assert.Throws<InvalidOperationException>(() => Monster(Modifier(null, 0.5f)).Validate());
            Assert.Throws<InvalidOperationException>(() => Monster(Modifier("1", 0.5f)).Validate(), "숫자 Target");
            Assert.Throws<InvalidOperationException>(() => Monster(Modifier("KnockbackDistance", null)).Validate(), "Multiplier 누락");
            Assert.Throws<InvalidOperationException>(() => Monster(Modifier("KnockbackDistance", -0.1f)).Validate());
            Assert.Throws<InvalidOperationException>(() => Monster(Modifier("BurnDuration", float.NaN)).Validate());
            Assert.Throws<InvalidOperationException>(() => Monster(Modifier("BurnDuration", float.PositiveInfinity)).Validate());
        }

        [Test]
        public void Builder_CombinesModifiers_AndMultipliesDuplicates()
        {
            var monster = Monster(Modifier("KnockbackDistance", 0.5f), Modifier("KnockbackDistance", 0.5f), Modifier("BurnDuration", 4f));

            var modifiers = PassiveBuilder.BuildModifiers(monster);

            Assert.AreEqual(0.25f, modifiers.KnockbackMultiplier, 0.0001f);
            Assert.AreEqual(4f, modifiers.BurnDurationMultiplier, 0.0001f);
        }

        [Test]
        public void Builder_WithoutModifiers_ReturnsNone()
        {
            Assert.AreSame(EffectModifiers.None, PassiveBuilder.BuildModifiers(Monster()));
            Assert.AreSame(EffectModifiers.None, PassiveBuilder.BuildModifiers(Monster(Immune("Stun"))));
            Assert.IsNull(PassiveBuilder.BuildPassives(Monster(Modifier("BurnDuration", 2f))), "Modifier는 패시브 객체가 아니다");
        }

        [Test]
        public void EffectModifiers_RejectsInvalidValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new EffectModifiers(-1f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EffectModifiers(1f, float.NaN));
        }

        // ───────── 밀치기 저항 ─────────

        [Test]
        public void Knockback_IsScaledByMultiplier()
        {
            var half = Model(modifiers: new EffectModifiers(0.5f, 1f));
            var immune = Model(modifiers: new EffectModifiers(0f, 1f));
            var normal = Model();

            Assert.AreEqual(0.5f, half.ResolveKnockback(Vector2.up, 1f).y, 0.0001f);
            Assert.AreEqual(Vector2.zero, immune.ResolveKnockback(Vector2.up, 1f));
            Assert.AreEqual(1f, normal.ResolveKnockback(Vector2.up, 1f).y, 0.0001f);
        }

        [Test(Description = "실제 Enemy도 저항한 만큼만 밀려난다")]
        public void Enemy_IsPushedByResolvedDistance()
        {
            var enemy = TestEnemy.Create(Model(modifiers: new EffectModifiers(0.5f, 1f)), new Vector2(0f, 10f));

            enemy.ApplyKnockback(Vector2.up, 2f);

            Assert.AreEqual(11f, enemy.Position.y, 0.0001f);
        }

        // ───────── 점화 지속시간 ─────────

        [Test]
        public void BurnDuration_IsScaledByMultiplier()
        {
            var long4 = Model(modifiers: new EffectModifiers(1f, 4f));
            var normal = Model();

            long4.ApplyBurn(5f, 2f);
            normal.ApplyBurn(5f, 2f);

            Assert.AreEqual(8f, long4.BurnRemaining, 0.0001f);
            Assert.AreEqual(2f, normal.BurnRemaining, 0.0001f);
        }

        [Test(Description = "배수가 0이면 점화에 걸리지 않는다")]
        public void BurnDuration_ZeroMultiplierIgnoresBurn()
        {
            var model = Model(modifiers: new EffectModifiers(1f, 0f));

            model.ApplyBurn(5f, 2f);

            Assert.AreEqual(0f, model.BurnRemaining);
        }

        [Test(Description = "늘어난 지속시간만큼 점화 피해가 더 들어간다")]
        public void BurnDuration_LongerBurnDealsMoreTicks()
        {
            var model = Model(modifiers: new EffectModifiers(1f, 3f));
            model.ApplyBurn(5f, 2f); // 6초

            for (int i = 0; i < 6; i++)
            {
                model.TickStatus(1f);
            }

            Assert.AreEqual(100 - 6 * 5, model.Hp);
            Assert.AreEqual(0f, model.BurnRemaining);
        }

        // ───────── 실제 데이터 ─────────
    }
}
