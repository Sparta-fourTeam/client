using System;
using System.Collections.Generic;
using Game.Core;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>자식 스킬 시전 효과(onEvent, periodic)의 컴파일, 적용, 카탈로그 검증</summary>
    public sealed class SkillChildEffectTests
    {
        private sealed class FakeCaster : IChildSkillCaster
        {
            public readonly List<int> Casts = new List<int>();
            public readonly List<Vector2> Positions = new List<Vector2>();
            public readonly List<float> Scales = new List<float>();
            public void Cast(int skillId, AttackContext context, float damageScale) { Casts.Add(skillId); Positions.Add(context.Position); Scales.Add(damageScale); }
        }

        private static SkillConfigBuilder Builder(IChildSkillCaster caster) => new SkillConfigBuilder(
            SkillConfig.FromDefinition(new WeaponData { baseStats = new WeaponBaseStats() }).WithChildCaster(caster));

        private static EffectDef OnEvent(AttackEvent trigger, int skillId, float chance = 1, int count = 1) =>
            new EffectDef { kind = "onEvent", trigger = trigger, skillId = skillId, chance = chance, count = count };

        [Test]
        public void OnEvent_CastsChildAtEventPositionWhenTriggerRaised()
        {
            var caster = new FakeCaster();
            var builder = Builder(caster);
            Assert.IsTrue(builder.TryApplyCatalog(new[] { OnEvent(AttackEvent.Expired, 7, count: 2) }));

            var reactions = builder.Build().Reactions;
            reactions.Raise(AttackEvent.Hit, new AttackContext(Vector2.zero, Vector3.up));
            Assert.AreEqual(0, caster.Casts.Count, "다른 시점에는 시전하지 않는다");
            reactions.Raise(AttackEvent.Expired, new AttackContext(new Vector2(3, 4), Vector3.up));
            Assert.AreEqual(new[] { 7, 7 }, caster.Casts);
            Assert.AreEqual(new Vector2(3, 4), caster.Positions[0]);
        }

        [Test]
        public void OnEvent_RollsChance()
        {
            var caster = new FakeCaster();
            var builder = Builder(caster);
            Assert.IsTrue(builder.TryApplyCatalog(new[] { OnEvent(AttackEvent.Hit, 7, chance: .3f) }));
            var reactions = builder.Build().Reactions;

            reactions.Raise(AttackEvent.Hit, new AttackContext(Vector2.zero, Vector3.up, randomValue: () => .29f));
            reactions.Raise(AttackEvent.Hit, new AttackContext(Vector2.zero, Vector3.up, randomValue: () => .3f));
            Assert.AreEqual(1, caster.Casts.Count);
        }

        [Test]
        public void Periodic_CastsEveryIntervalOfAttackLocalTime()
        {
            var caster = new FakeCaster();
            var builder = Builder(caster);
            Assert.IsTrue(builder.TryApplyCatalog(new[]
            {
                new EffectDef { kind = "periodic", skillId = 9, interval = 1 }
            }));
            var reactions = builder.Build().Reactions;

            reactions.Raise(AttackEvent.Tick, new AttackContext(Vector2.zero, Vector3.up, deltaTime: .6f, elapsed: .6f));
            Assert.AreEqual(0, caster.Casts.Count);
            reactions.Raise(AttackEvent.Tick, new AttackContext(Vector2.zero, Vector3.up, deltaTime: .6f, elapsed: 1.2f));
            Assert.AreEqual(1, caster.Casts.Count);
        }

        [Test]
        public void ChildCast_FailsLoudlyWhenNoCasterIsConnected()
        {
            Assert.IsFalse(Builder(null).TryApplyCatalog(new[] { OnEvent(AttackEvent.Hit, 7) }),
                "시전기가 없으면 효과가 조용히 사라지지 않고 강화가 실패해야 한다");
        }

        [TestCase(0, 1f, 1)]
        [TestCase(7, 0f, 1)]
        [TestCase(7, 1.5f, 1)]
        [TestCase(7, 1f, 0)]
        public void OnEvent_RejectsInvalidFields(int skillId, float chance, int count)
        {
            Assert.IsFalse(Builder(new FakeCaster()).TryApplyCatalog(new[] { OnEvent(AttackEvent.Hit, skillId, chance, count) }));
        }

        [Test]
        public void OnEvent_RejectsTickAndPeriodicRejectsMissingInterval()
        {
            Assert.IsFalse(Builder(new FakeCaster()).TryApplyCatalog(new[] { OnEvent(AttackEvent.Tick, 7) }), "Tick은 periodic으로 쓴다");
            Assert.IsFalse(Builder(new FakeCaster()).TryApplyCatalog(new[] { new EffectDef { kind = "periodic", skillId = 7 } }));
        }

        [Test]
        public void Json_DeserializesChildCastFieldsWithDefaults()
        {
            var effect = JsonConvert.DeserializeObject<EffectDef>("{\"kind\":\"onEvent\",\"trigger\":\"Expired\",\"skillId\":2}");
            Assert.AreEqual(AttackEvent.Expired, effect.trigger);
            Assert.AreEqual(2, effect.skillId);
            Assert.AreEqual(1f, effect.chance);
            Assert.AreEqual(1, effect.count);
        }

        // ── 카탈로그 검증 ──────────────────────────────────────────────

        private static List<WeaponData> Catalog() =>
            JsonConvert.DeserializeObject<List<WeaponData>>(Resources.Load<TextAsset>("MockData/Weapons").text);

        private static void AddChildCast(WeaponData weapon, int childId) =>
            weapon.upgrades[0].effects.Add(OnEvent(AttackEvent.Hit, childId));

        [Test]
        public void Validator_AcceptsChildSkillThatExists()
        {
            var catalog = Catalog();
            AddChildCast(catalog[0], catalog[1].id);
            Assert.DoesNotThrow(() => WeaponCatalogValidator.Validate(catalog));
        }

        [Test]
        public void Validator_RejectsUnknownChildSkillWithCardId()
        {
            var catalog = Catalog();
            AddChildCast(catalog[0], 9999);
            var error = Assert.Throws<InvalidOperationException>(() => WeaponCatalogValidator.Validate(catalog));
            StringAssert.Contains(catalog[0].upgrades[0].id, error.Message);
            StringAssert.Contains("9999", error.Message);
        }

        [Test]
        public void Validator_RejectsSelfCast()
        {
            var catalog = Catalog();
            AddChildCast(catalog[0], catalog[0].id);
            var error = Assert.Throws<InvalidOperationException>(() => WeaponCatalogValidator.Validate(catalog));
            StringAssert.Contains("순환", error.Message);
        }

        [Test]
        public void Validator_RejectsCycleBetweenTwoSkills()
        {
            var catalog = Catalog();
            AddChildCast(catalog[0], catalog[1].id);
            AddChildCast(catalog[1], catalog[0].id);
            var error = Assert.Throws<InvalidOperationException>(() => WeaponCatalogValidator.Validate(catalog));
            StringAssert.Contains("순환", error.Message);
            StringAssert.Contains($"{catalog[0].id} → {catalog[1].id} → {catalog[0].id}", error.Message);
        }

        [Test]
        public void Validator_FindsCycleInsideVariantEffects()
        {
            var catalog = Catalog();
            var card = catalog[0].upgrades[0];
            card.variants = new[]
            {
                new WeaponUpgradeVariant { minPermanentLevel = 1, name = "v", desc = "v", effects = new List<EffectDef> { OnEvent(AttackEvent.Hit, catalog[0].id) } }
            };
            Assert.Throws<InvalidOperationException>(() => WeaponCatalogValidator.Validate(catalog));
        }
    }
}
