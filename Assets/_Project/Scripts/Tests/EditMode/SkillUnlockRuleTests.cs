using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class SkillUnlockRuleTests
    {
        private static List<SkillData> Skills() => new()
        {
            new SkillData { id = 1, unlockLevel = 1 },
            new SkillData { id = 2, unlockLevel = 3 },
            new SkillData { id = 3, unlockLevel = 5 },
        };

        [TestCase(1, new[] { 1 })]
        [TestCase(2, new[] { 1 })]
        [TestCase(3, new[] { 1, 2 })]
        [TestCase(5, new[] { 1, 2, 3 })]
        [TestCase(50, new[] { 1, 2, 3 })]
        public void UnlockedSkillIds_FollowPlayerLevel(int level, int[] expected)
        {
            CollectionAssert.AreEquivalent(expected, SkillUnlockRule.UnlockedSkillIds(Skills(), level));
        }

        [Test(Description = "해금 레벨을 쓰지 않은 스킬은 처음부터 열려 있다")]
        public void UnlockLevel_DefaultsToOne()
        {
            Assert.AreEqual(1, new SkillData().unlockLevel);
        }

        [Test(Description = "MockData의 모든 스킬은 해금 레벨이 1 이상이고, 랜덤 스킬재료의 스킬이 레벨 1에서 최소 하나는 열려 있다")]
        public void MockData_HasValidUnlockLevels_AndAtLeastOneOpenMaterialSkill()
        {
            var data = new GameDataStore();
            Assert.IsTrue(data.LoadSkills().All(skill => skill.unlockLevel >= 1));

            var open = SkillUnlockRule.UnlockedSkillIds(data, 0);
            var materialSkillIds = ItemIds.SkillMaterials
                .Select(id => int.Parse(data.Items.GetOrThrow(id).TargetId)).ToList();
            Assert.IsTrue(open.Any(materialSkillIds.Contains));
        }

        [Test(Description = "해금 레벨이 1 미만인 스킬 정의는 부팅 때 거절한다")]
        public void Validator_RejectsUnlockLevelBelowOne()
        {
            var skills = new GameDataStore().LoadSkills();
            skills[0].unlockLevel = 0;

            Assert.Throws<InvalidOperationException>(() => SkillCatalogValidator.Validate(skills));
        }
    }
}
