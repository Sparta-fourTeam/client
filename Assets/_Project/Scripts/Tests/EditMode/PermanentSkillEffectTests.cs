using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>스킬 영구 강화 효과가 레벨만큼 쌓여 기본 설정에 적용되고, 잘못된 효과 데이터는 부팅 때 거부되는지 본다</summary>
    public sealed class PermanentSkillEffectTests
    {
        private GameDataStore _data;
        private SkillData _shuriken;

        [SetUp]
        public void SetUp()
        {
            _data = new GameDataStore();
            _shuriken = _data.LoadSkills().First(s => s.progressionId == "shuriken");
        }

        private static UpgradeDefinition Row(string upgradeId, params UpgradeStatEffect[] effects) => new UpgradeDefinition
        {
            UpgradeId = upgradeId,
            MaxLevel = 30,
            Effects = effects.ToList(),
        };

        private static UpgradeStatEffect Effect(string kind, float valuePerLevel) => new UpgradeStatEffect { Kind = kind, ValuePerLevel = valuePerLevel };

        [Test(Description = "레벨 0이면 효과가 없다")]
        public void At_LevelZero_IsEmpty()
        {
            Assert.IsEmpty(PermanentSkillEffect.At(Row("shuriken", Effect("damage", 8)), 0));
        }

        [Test(Description = "레벨 L의 효과량은 ValuePerLevel × L이다")]
        public void At_Level_MultipliesValuePerLevel()
        {
            var effects = PermanentSkillEffect.At(Row("shuriken", Effect("damage", 8), Effect("attackSpeed", 2)), 5);

            Assert.AreEqual(2, effects.Count);
            Assert.AreEqual("damage", effects[0].kind);
            Assert.AreEqual(40f, effects[0].value);
            Assert.AreEqual("attackSpeed", effects[1].kind);
            Assert.AreEqual(10f, effects[1].value);
        }

        [Test(Description = "효과는 기본 스탯에 카드와 같은 규칙으로 적용된다 (damage 40 → 피해 ×1.4), 효과가 없으면 기본 스탯 그대로다")]
        public void Apply_ChangesBaseStats()
        {
            float baseDamage = SkillConfig.FromDefinition(_shuriken).Stats.Cast.Damage;

            var raised = PermanentSkillEffect.Apply(_shuriken, new List<EffectDef> { new EffectDef { kind = "damage", value = 40 } });
            var plain = PermanentSkillEffect.Apply(_shuriken, null);

            Assert.AreEqual(baseDamage * 1.4f, raised.Stats.Cast.Damage, 0.001f);
            Assert.AreEqual(baseDamage, plain.Stats.Cast.Damage, 0.001f);
        }

        [Test(Description = "스킬 강화 행의 효과 키가 EffectRegistry에 없으면 거부한다")]
        public void Validate_RejectsUnregisteredKind()
        {
            Assert.Throws<InvalidOperationException>(() =>
                PermanentSkillEffect.Validate(Row("shuriken", Effect("skillDamagePercent", 5)), new[] { _shuriken }));
        }

        [Test(Description = "그 스킬의 공격 종류가 쓰지 않는 효과는 조용히 무시되므로 거부한다")]
        public void Validate_RejectsUnsupportedKind()
        {
            Assert.IsFalse(EffectRegistry.Supports(_shuriken.castType, "areaRadius"), "전제: 쿠나이는 영역 효과를 쓰지 않는다");

            Assert.Throws<InvalidOperationException>(() =>
                PermanentSkillEffect.Validate(Row("shuriken", Effect("areaRadius", 5)), new[] { _shuriken }));
        }

        [Test(Description = "장비처럼 스킬과 연결되지 않은 행은 효과 키를 검사하지 않는다")]
        public void Validate_IgnoresRowsWithoutSkill()
        {
            Assert.DoesNotThrow(() =>
                PermanentSkillEffect.Validate(Row("equipment.hat", Effect("skillDamagePercent", 5)), new[] { _shuriken }));
        }

        [Test(Description = "실제 Upgrades 데이터의 스킬 강화 효과는 모두 그 스킬에 적용된다")]
        public void UpgradesData_SkillEffectsAreValid()
        {
            var skills = _data.LoadSkills();
            foreach (var skill in skills.Where(s => !string.IsNullOrEmpty(s.progressionId) && _data.Upgrades.Contains(s.progressionId)))
            {
                Assert.DoesNotThrow(() => PermanentSkillEffect.Validate(_data.Upgrades.GetOrThrow(skill.progressionId), skills), skill.progressionId);
            }
        }

        [Test(Description = "프로필의 강화 레벨만큼 효과를 돌려주고, 강화 데이터가 없는 스킬은 빈 목록이다")]
        public void ProfileEffects_FollowUpgradeLevel()
        {
            var profile = new PlayerProfile(_data);
            var effects = new ProfilePermanentSkillEffects(profile, _data);
            var def = _data.Upgrades.GetOrThrow("shuriken");

            Assert.IsEmpty(effects.For("shuriken"));

            profile.Apply(new PlayerSnapshot { upgrades = new() { new UpgradeRow { upgradeId = "shuriken", level = 5 } } });

            var raised = effects.For("shuriken");
            Assert.AreEqual(def.Effects.Count, raised.Count);
            Assert.AreEqual(def.Effects[0].ValuePerLevel * 5, raised[0].value);
            Assert.IsEmpty(effects.For("frost"));
            Assert.IsEmpty(effects.For(null));
        }
    }
}
