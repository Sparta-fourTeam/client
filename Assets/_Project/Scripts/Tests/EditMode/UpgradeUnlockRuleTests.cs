using System;
using System.Collections.Generic;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class UpgradeUnlockRuleTests
    {
        private static List<SkillData> Skills() => new()
        {
            new SkillData { id = 1, progressionId = "shuriken", unlockLevel = 1 },
            new SkillData { id = 3, progressionId = "lightning", unlockLevel = 3 },
            new SkillData { id = 4, unlockLevel = 4 },   // 영구 강화 연결 없음
        };

        private static UpgradeDefinition Row(string id, int unlockLevel = 1) => new() { UpgradeId = id, UnlockLevel = unlockLevel };

        [Test(Description = "스킬 강화는 스킬이 열리는 레벨을 따르고, 장비 같은 나머지는 행의 UnlockLevel을 쓴다")]
        public void LevelOf_SkillFollowsSkill_OthersUseOwnRow()
        {
            var levels = UpgradeUnlockRule.SkillUnlockLevels(Skills());

            Assert.AreEqual(3, UpgradeUnlockRule.LevelOf(Row("lightning"), levels));
            Assert.AreEqual(1, UpgradeUnlockRule.LevelOf(Row("shuriken"), levels));
            Assert.AreEqual(5, UpgradeUnlockRule.LevelOf(Row("equipment.weapon", 5), levels));
        }

        [Test(Description = "스킬 강화 행이 자기 UnlockLevel을 따로 정하면 거절한다 (출처가 둘이 되지 않게)")]
        public void Validate_SkillRowWithOwnUnlockLevel_Throws()
        {
            var levels = UpgradeUnlockRule.SkillUnlockLevels(Skills());

            Assert.DoesNotThrow(() => UpgradeUnlockRule.Validate(Row("lightning"), levels));
            Assert.DoesNotThrow(() => UpgradeUnlockRule.Validate(Row("equipment.hat", 2), levels));
            Assert.Throws<InvalidOperationException>(() => UpgradeUnlockRule.Validate(Row("lightning", 5), levels));
        }

        [Test(Description = "같은 progressionId를 여러 스킬이 쓰면 가장 먼저 열리는 레벨을 따른다")]
        public void SharedProgressionId_UsesEarliestLevel()
        {
            var skills = new List<SkillData>
            {
                new() { id = 1, progressionId = "x", unlockLevel = 6 },
                new() { id = 2, progressionId = "x", unlockLevel = 2 },
            };

            Assert.AreEqual(2, UpgradeUnlockRule.SkillUnlockLevels(skills)["x"]);
        }
    }
}
