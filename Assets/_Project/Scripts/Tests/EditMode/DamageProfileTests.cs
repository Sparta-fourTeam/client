using System;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Combat;
using Game.Core.Messages;
using MessagePipe;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    // 속성·시전 형태 저항(DamageProfile)과 그것을 쓰는 적의 피해 파이프라인, 투사체 차단
    public sealed class DamageProfileTests
    {
        private static readonly EnemyAttackStats Attack = new EnemyAttackStats(AttackType.Melee, 1, 1f, 0f);

        private sealed class Recorder<T> : IPublisher<T>
        {
            public readonly List<T> Messages = new();
            public void Publish(T message) => Messages.Add(message);
        }

        private sealed class Provider : IEnemyTargetProvider
        {
            public readonly List<IEnemyTarget> Targets = new();

            public int GetNearest(Vector2 from, int count, List<IEnemyTarget> results)
            {
                results.Clear();
                results.AddRange(Targets);
                return results.Count;
            }
        }

        private static MonsterDefinition Monster(
            Dictionary<string, float> resists = null, Dictionary<string, float> castResists = null, bool blocksProjectile = false) =>
            new MonsterDefinition
            {
                Id = 7,
                Hp = 10,
                AttackInterval = 1f,
                Resists = resists ?? new Dictionary<string, float>(),
                CastResists = castResists ?? new Dictionary<string, float>(),
                BlocksProjectile = blocksProjectile,
            };

        private static DamageProfile Profile(Dictionary<string, float> resists = null, Dictionary<string, float> castResists = null, bool blocks = false) =>
            DamageProfile.From(Monster(resists, castResists, blocks));

        private static DamageInfo Skill(int amount, Element element = Element.Neutral, CastType cast = CastType.Projectile, bool impact = false) =>
            new DamageInfo(amount, 1, element, cast, impact);

        // ───────── 속성 약점·저항 ─────────

        // ───────── 시전 형태 ─────────

        // ───────── 스킬 밖의 피해 ─────────

        // ───────── 투사체 차단 ─────────

        // ───────── 검증 ─────────

        [Test]
        public void Validate_RejectsUnknownKeysAndBadRatios()
        {
            Assert.Throws<InvalidOperationException>(() => Monster(new Dictionary<string, float> { { "Fire2", 0.5f } }).Validate());
            Assert.Throws<InvalidOperationException>(() => Monster(new Dictionary<string, float> { { "fire", 0.5f } }).Validate(), "이름은 대소문자를 구분한다");
            Assert.Throws<InvalidOperationException>(() => Monster(new Dictionary<string, float> { { "99", 0.5f } }).Validate(), "숫자 키는 허용하지 않는다");
            Assert.Throws<InvalidOperationException>(() => Monster(new Dictionary<string, float> { { "Fire", -1.5f } }).Validate());
            Assert.Throws<InvalidOperationException>(() => Monster(castResists: new Dictionary<string, float> { { "Beam2", 0.1f } }).Validate());
            Assert.Throws<InvalidOperationException>(() => Monster(new Dictionary<string, float> { { "Fire", float.NaN } }).Validate());
            Assert.DoesNotThrow(() => Monster(new Dictionary<string, float> { { "Fire", -1f }, { "Earth", 2f } },
                new Dictionary<string, float> { { "Chain", 0.25f } }, true).Validate());
        }

        // ───────── 적의 피해 파이프라인 ─────────

        private static EnemyModel Model(DamageProfile profile, Recorder<EnemyHpChanged> hp = null, int maxHp = 100) =>
            new EnemyModel(1, 0f, EnemyType.Normal, maxHp, Attack, hp ?? new Recorder<EnemyHpChanged>(), new Recorder<EnemyDied>(),
                damageProfile: profile);

        [Test]
        public void Model_AppliesProfileToSkillDamage()
        {
            var model = Model(Profile(new Dictionary<string, float> { { "Fire", 0.5f } }));

            model.TakeDamage(Skill(10, Element.Fire));

            Assert.AreEqual(85, model.Hp);
        }

        [Test(Description = "int 피해(스킬 밖)는 속성 계산을 받지 않는다")]
        public void Model_IntDamageIgnoresProfile()
        {
            var model = Model(Profile(new Dictionary<string, float> { { "Neutral", -1f } }, blocks: true));

            model.TakeDamage(10);

            Assert.AreEqual(90, model.Hp);
        }

        [Test(Description = "저항을 먼저 적용하고 취약 배율은 그 뒤에 곱한다")]
        public void Model_AppliesVulnerabilityAfterProfile()
        {
            var model = Model(Profile(new Dictionary<string, float> { { "Fire", 0.5f } }));
            model.ApplyVulnerability(0.5f, 5f);

            model.TakeDamage(Skill(10, Element.Fire)); // 10 → 15 → 22

            Assert.AreEqual(78, model.Hp);
        }

        [Test(Description = "막힌 피해는 HP 변화 메시지도 내지 않는다")]
        public void Model_NegatedDamageDoesNotPublishHpChange()
        {
            var hp = new Recorder<EnemyHpChanged>();
            var model = Model(Profile(new Dictionary<string, float> { { "Earth", -1f } }), hp);

            model.TakeDamage(Skill(10, Element.Earth));

            Assert.AreEqual(100, model.Hp);
            Assert.IsEmpty(hp.Messages);
        }

        [Test(Description = "점화 지속 피해는 속성 면역과 무관하게 들어간다")]
        public void Model_BurnTicksIgnoreElementImmunity()
        {
            var model = Model(Profile(new Dictionary<string, float> { { "Fire", -1f } }));
            model.ApplyBurn(10f, 3f);

            model.TickStatus(1f);

            Assert.AreEqual(90, model.Hp);
        }

        // ───────── 투사체 차단 몬스터 ─────────

        [Test(Description = "차단 몬스터에 맞은 투사체는 충격 피해가 0이지만 상태이상 같은 부가 반응은 걸리고, 관통은 막힌다")]
        public void BlockingEnemy_NegatesImpactKeepsReactionsAndStopsPierce()
        {
            var go = new GameObject("BlockingEnemyProjectile");
            try
            {
                var blockerModel = Model(Profile(blocks: true));
                var behindModel = Model(DamageProfile.None);
                var provider = new Provider();
                provider.Targets.Add(TestEnemy.Create(blockerModel, Vector2.right * 0.5f));
                provider.Targets.Add(TestEnemy.Create(behindModel, Vector2.right * 0.7f));

                var projectile = go.AddComponent<Projectile>();
                var pool = new UnityEngine.Pool.ObjectPool<Projectile>(() => projectile);
                using (DamageAttribution.Begin(new DamageSource(2, Element.Neutral, CastType.Projectile)))
                {
                    pool.Get().Init(pool, Vector3.zero, Vector3.right, 10, 10, 3, provider, pierceCount: 2,
                        burnDuration: 3f, burnDamage: 5f);
                }

                typeof(Projectile).GetMethod("Tick", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .Invoke(projectile, new object[] { 0.1f });

                Assert.AreEqual(100, blockerModel.Hp, "직접 충격 피해는 무효");
                Assert.Greater(blockerModel.BurnRemaining, 0f, "부가 반응(점화)은 걸린다");
                Assert.AreEqual(100, behindModel.Hp, "관통하지 못해 뒤의 적은 맞지 않는다");
                Assert.AreEqual(0f, behindModel.BurnRemaining);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}
