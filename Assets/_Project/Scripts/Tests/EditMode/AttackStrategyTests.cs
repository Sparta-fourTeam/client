using System;
using System.Collections.Generic;
using System.Linq;
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
