using System;
using System.Collections.Generic;
using Game.Core;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>전략 등록 검증과 자식 스킬의 카드 후보 제외</summary>
    public sealed class AttackStrategyTests
    {
        [Test]
        public void Validator_RejectsCastTypeWithoutRegisteredStrategy()
        {
            var catalog = JsonConvert.DeserializeObject<List<SkillData>>(Resources.Load<TextAsset>("MockData/Skills").text);
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
