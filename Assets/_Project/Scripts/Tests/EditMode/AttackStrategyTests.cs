using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>전략 등록 검증과 자식 스킬의 카드 후보 제외</summary>
    public sealed class AttackStrategyTests
    {
        [TestCase(SkillForm.ThunderArrow)]
        [TestCase(SkillForm.Enbakutsu)]
        public void Projectile_FormUsesOwnPrefabAndSharesReservations(SkillForm form)
        {
            var basic = new GameObject("FormPoolBaseTest");
            var thunder = new GameObject("FormPoolThunderTest");
            basic.AddComponent<Projectile>();
            thunder.AddComponent<Projectile>();
            thunder.transform.localScale = new Vector3(2, 3, 1);
            var provider = new FormProvider();
            var reservations = new TargetReservations();
            SkillCaster caster = null;
            Projectile[] flights = null;
            try
            {
                var data = new SkillData
                {
                    castType = CastType.Projectile,
                    baseStats = new SkillBaseStats
                    { cast = { baseDamage = 10, range = 5, projectileCount = 1 }, projectile = { speed = 10 } }
                };
                caster = SkillFactory.Create(data, basic, basic.transform, provider, reservations: reservations, formPrefabs: new[]
                {
                    new SkillFormPrefabEntry { form = form, prefab = thunder }
                });
                caster.FireAt(Vector3.zero);
                Assert.IsTrue(reservations.IsLethal(provider.Target), "기본형 발사도 공유 장부에 예약한다.");
                var builder = new SkillConfigBuilder(caster.Config);
                Assert.IsTrue(builder.TryApply(new IUpgradeEffect[] { new TransformUpgradeEffect(form) }));
                caster.FireAt(Vector3.zero, _ => builder.Build());
                Assert.AreEqual(10, reservations.Overkill(provider.Target), "변환형 발사도 같은 장부에 예약한다.");
                flights = UnityEngine.Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None)
                    .Where(p => p.name == "FormPoolBaseTest(Clone)" || p.name == "FormPoolThunderTest(Clone)").ToArray();
                Assert.AreEqual(2, flights.Length, "변환 전에 발사한 기본형도 유지한다.");
                Assert.AreEqual(thunder.transform.localScale,
                    flights.Single(p => p.name == "FormPoolThunderTest(Clone)").transform.localScale);
                caster.Dispose();
                Assert.IsTrue(flights.All(p => p == null), "전투 정리 시 형태별 비행 중 객체도 제거한다.");
            }
            finally
            {
                caster?.Dispose();
                UnityEngine.Object.DestroyImmediate(basic);
                UnityEngine.Object.DestroyImmediate(thunder);
            }
        }

        private sealed class FormTarget : IEnemyTarget
        {
            public Vector2 Position => Vector2.up;
            public int CurrentHp => 10;
            public void TakeDamage(int damage) { }
        }

        [Test]
        public void Hitscan_FormUsesOwnPrefabAndRestoresBasicPoolWithoutReplacingActiveStrikes()
        {
            var basic = new GameObject("HitscanBaseTest");
            var form = new GameObject("HitscanFormTest");
            basic.AddComponent<HitscanEffect>();
            form.AddComponent<HitscanEffect>();
            form.transform.localScale = new Vector3(2, 3, 1);
            SkillCaster caster = null;
            try
            {
                var data = new SkillData { castType = CastType.Instant, baseStats = new SkillBaseStats { cast = { range = 10 } } };
                caster = SkillFactory.Create(data, basic, basic.transform, new FormProvider(), formPrefabs: new[]
                {
                    new SkillFormPrefabEntry { form = SkillForm.JudgementThunder, prefab = form }
                });
                caster.FireAt(Vector3.zero);
                var original = UnityEngine.Object.FindObjectsByType<HitscanEffect>(FindObjectsSortMode.None)
                    .Single(p => p.name == "HitscanBaseTest(Clone)");
                caster.FireAt(Vector3.zero, config =>
                {
                    var builder = new SkillConfigBuilder(config);
                    Assert.IsTrue(builder.TryApply(new IUpgradeEffect[] { new TransformUpgradeEffect(SkillForm.JudgementThunder) }));
                    return builder.Build();
                });
                var upgraded = UnityEngine.Object.FindObjectsByType<HitscanEffect>(FindObjectsSortMode.None)
                    .Single(p => p.name == "HitscanFormTest(Clone)");
                Assert.IsTrue(original.gameObject.activeSelf, "기존 타격은 변환과 무관하게 끝까지 유지한다.");
                Assert.AreEqual(form.transform.localScale * 1.5f, upgraded.transform.localScale);
                var basicPool = ((HitscanStrategy)caster.Strategy).Pool;
                basicPool.Release(original);
                caster.FireAt(Vector3.zero);
                Assert.IsTrue(original.gameObject.activeSelf, "기본형으로 돌아오면 기존 기본 풀을 재사용한다.");
                Assert.AreEqual(basic.transform.localScale, original.transform.localScale);
                caster.Dispose();
                Assert.IsTrue(original == null && upgraded == null, "전투 정리 시 두 형태의 진행 중 타격을 모두 제거한다.");
            }
            finally
            {
                caster?.Dispose();
                UnityEngine.Object.DestroyImmediate(basic);
                UnityEngine.Object.DestroyImmediate(form);
            }
        }

        private sealed class FormProvider : IEnemyTargetProvider
        {
            public readonly FormTarget Target = new();
            public int GetNearest(Vector2 from, int count, List<IEnemyTarget> results)
            {
                results.Clear();
                if (count > 0) { results.Add(Target); }
                return results.Count;
            }
        }

        [TestCase(SkillForm.Default)]
        [TestCase(SkillForm.JudgementThunder)]
        public void Hitscan_CheckpointBeforeHit_PreservesPrimaryDamageStatusAndForm(SkillForm form)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var basic = new GameObject("CheckpointHitscanBase");
            var transformed = new GameObject("CheckpointHitscanForm");
            var origin = new Vector2(1000, 1000);
            var model = new EnemyModel(1, 0, EnemyType.Normal, 100,
                new EnemyAttackStats(AttackType.Melee, 0, 1, 0),
                new SilentPublisher<Game.Core.Messages.EnemyHpChanged>(), new SilentPublisher<Game.Core.Messages.EnemyDied>());
            var enemy = TestEnemy.Create(model, origin);
            enemy.gameObject.layer = 31;
            enemy.gameObject.AddComponent<CircleCollider2D>();
            foreach (var prefab in new[] { basic, transformed })
            {
                var effect = prefab.AddComponent<HitscanEffect>();
                typeof(HitscanEffect).GetField("targetMask", flags).SetValue(effect, (LayerMask)(1 << 31));
            }
            var entries = new[] { new SkillFormPrefabEntry { form = SkillForm.JudgementThunder, prefab = transformed } };
            var provider = new CheckpointTargetProvider(enemy);
            var definition = new SkillData
            {
                castType = CastType.Instant,
                baseStats = new SkillBaseStats
                { cast = { baseDamage = 17, range = 5, projectileCount = 1 }, status = { paralysisDuration = 2 } }
            };
            var config = SkillConfig.FromDefinition(definition);
            if (form != SkillForm.Default)
            {
                var builder = new SkillConfigBuilder(config);
                Assert.That(builder.TryApply(new IUpgradeEffect[] { new TransformUpgradeEffect(form) }), Is.True);
                config = builder.Build();
            }
            var original = new HitscanStrategy(basic, entries);
            var restored = new HitscanStrategy(basic, entries);
            try
            {
                original.Fire(config, new AttackEnvironment(basic.transform, provider, (_, _) => new[] { enemy }, origin));
                var state = new SkillRuntimeState();
                typeof(HitscanStrategy).GetMethod("CaptureRuntime", flags).Invoke(original, new object[] { state });
                original.Dispose();
                var contextType = typeof(SkillConfig).Assembly.GetType("Game.Core.AttackRestoreContext");
                var context = Activator.CreateInstance(contextType, true);
                contextType.GetField("Targets", flags).SetValue(context, provider);
                contextType.GetField("Enemy", flags).SetValue(context, new Func<int, IEnemyTarget>(id => id == enemy.Id ? enemy : null));
                typeof(HitscanStrategy).GetMethod("RestoreRuntime", flags).Invoke(restored, new[] { state, context });
                var strike = UnityEngine.Object.FindObjectsByType<HitscanEffect>(FindObjectsSortMode.None)
                    .Single(e => e.name.EndsWith("(Clone)"));
                Physics2D.SyncTransforms();
                typeof(HitscanEffect).GetMethod("Hit", flags).Invoke(strike, null);
                typeof(HitscanEffect).GetMethod("Hit", flags).Invoke(strike, null);
                Assert.That(model.Hp, Is.EqualTo(83), "적중 전 저장한 기본 타격은 복원 후 정확히 한 번 피해를 준다");
                Assert.That(model.ParalysisRemaining, Is.EqualTo(2), "카드 반응이 없어도 기본 마비를 복원한다");
                Assert.That(strike.name, Is.EqualTo((form == SkillForm.Default ? basic.name : transformed.name) + "(Clone)"));
            }
            finally
            {
                restored.Dispose();
                original.Dispose();
                UnityEngine.Object.DestroyImmediate(enemy.gameObject);
                UnityEngine.Object.DestroyImmediate(basic);
                UnityEngine.Object.DestroyImmediate(transformed);
            }
        }

        private sealed class SilentPublisher<T> : MessagePipe.IPublisher<T>
        {
            public void Publish(T message) { }
        }

        private sealed class CheckpointTargetProvider : IEnemyTargetProvider
        {
            private readonly IEnemyTarget target;
            internal CheckpointTargetProvider(IEnemyTarget target) => this.target = target;
            public int GetNearest(Vector2 from, int count, List<IEnemyTarget> results)
            { results.Clear(); if (count > 0) { results.Add(target); } return results.Count; }
        }

        [Test]
        public void Validator_RejectsCastTypeWithoutRegisteredStrategy()
        {
            var catalog = new GameDataStore().LoadSkills();
            catalog[0].castType = (CastType)99;
            var error = Assert.Throws<InvalidOperationException>(() => SkillCatalogValidator.Validate(catalog));
            StringAssert.Contains(catalog[0].name, error.Message);
        }

        // ── 자식 스킬 시전기 ─────────────────────────────────────────

        [Test]
        public void ChildOnlySkill_IsNeverOfferedAsNewSkillCard()
        {
            var normal = new SkillData { id = 1, name = "일반", baseStats = new SkillBaseStats(), maxLevel = 5, upgrades = new List<SkillUpgradeOption>() };
            var child = new SkillData { id = 2, name = "자식", baseStats = new SkillBaseStats(), maxLevel = 5, upgrades = new List<SkillUpgradeOption>(), childOnly = true };
            var choices = new SkillUpgradeChoices(new List<SkillBase>(), new[] { normal, child }, new EmptyState(), _ => true);
            var offered = choices.Select(5, upper => 0);
            Assert.AreEqual(1, offered.Count);
            Assert.AreEqual(1, offered[0].newSkillData.id);
        }

        private sealed class EmptyState : IUpgradeState
        {
            public int GetWeaponLevel(int weaponId) => 0;
            public int GetPermanentWeaponLevel(int weaponId) => 0;
            public int GetAcquiredCount(int weaponId, string cardId) => 0;
        }
    }
}
