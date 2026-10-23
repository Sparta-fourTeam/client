using System.Linq;
using Game.Core;
using NUnit.Framework;
using UnityEngine.Localization.Settings;

namespace Game.Tests
{
    /// <summary>프로필 변경과 구독 종료 시 카탈로그의 해금 상태가 올바르게 갱신되는지 확인한다</summary>
    public sealed class DummyGrowthCatalogTests
    {
        private GameDataStore _data;
        private PlayerProfile _profile;
        private DummyGrowthCatalog _catalog;

        [SetUp]
        public void SetUp()
        {
            _data = new GameDataStore();
            _profile = new PlayerProfile(_data);
            _catalog = new DummyGrowthCatalog(_profile, _data);
        }

        [TearDown]
        public void TearDown() => _catalog.Dispose();

        // 레벨 level에 막 도달하는 누적 경험치
        private int ExpFor(int level)
        {
            int exp = 0;
            for (int l = 1; l < level; l++) { exp += _data.PlayerLevels.RequiredToNext(l); }
            return exp;
        }

        [Test(Description = "장비·스킬 해금 상태는 프로필 변경과 감소에도 각 선언된 경계를 따른다")]
        public void Unlocks_RecomputeWhenProfileChanges()
        {
            var locked = _catalog.Skills.First(s => s.UnlockLevel > _profile.Level);
            AssertUnlocks();
            _profile.Apply(new PlayerSnapshot { exp = ExpFor(locked.UnlockLevel) });
            Assert.IsTrue(locked.IsUnlocked);
            AssertUnlocks();
            _profile.Apply(new PlayerSnapshot { exp = 0 });
            Assert.IsFalse(locked.IsUnlocked);
            AssertUnlocks();
            _catalog.Dispose();
            _profile.Apply(new PlayerSnapshot { exp = ExpFor(locked.UnlockLevel) });
            Assert.IsFalse(locked.IsUnlocked, "종료된 카탈로그는 새 프로필을 따라가지 않는다");
        }

        [Test(Description = "언어 전환 때 보관 중인 스킬 객체의 이름·설명·능력치·보상이 갱신되고 수치는 유지된다. 종료 후에는 구독이 남지 않는다")]
        public void LocaleChangeRefreshesCachedSkillPresentationInPlace()
        {
            var original = LocalizationSettings.SelectedLocale;
            var skills = _catalog.Skills;
            var skill = skills.First(s => s.UpgradeId != null && s.LevelRewards.Count > 0);
            string name = skill.Name, description = skill.Description;
            string stat = skill.Stats[0].Label, reward = skill.LevelRewards[0].Text;
            var progression = (skill.Level, skill.MaxLevel, skill.CoinCost, skill.MaterialCost, skill.IsUnlocked);
            string damage = skill.Stats[0].Current;
            System.GC.Collect(); // 원래 반환 목록이 임시 LINQ 목록으로 대체돼 회수되는 회귀도 확인한다.
            try
            {
                LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.GetLocale("en");
                Assert.That(_catalog.Skills, Is.SameAs(skills));
                Assert.That(skills.First(s => s.Id == skill.Id), Is.SameAs(skill));
                Assert.That(skill.Name, Is.Not.EqualTo(name));
                Assert.That(skill.Description, Is.Not.EqualTo(description));
                Assert.That(skill.Stats[0].Label, Is.Not.EqualTo(stat));
                Assert.That(skill.LevelRewards[0].Text, Is.Not.EqualTo(reward));
                Assert.AreEqual(progression, (skill.Level, skill.MaxLevel, skill.CoinCost, skill.MaterialCost, skill.IsUnlocked));
                Assert.AreEqual(damage, skill.Stats[0].Current);

                string englishName = skill.Name;
                _catalog.Dispose();
                LocalizationSettings.SelectedLocale = original;
                Assert.AreEqual(englishName, skill.Name, "종료한 카탈로그는 언어 변경을 따라가지 않는다");
            }
            finally { LocalizationSettings.SelectedLocale = original; }
        }

        private void AssertUnlocks()
        {
            foreach (var skill in _catalog.Skills)
            {
                Assert.AreEqual(skill.UnlockLevel <= _profile.Level, skill.IsUnlocked, skill.UpgradeId);
            }
            foreach (var equip in _catalog.Equips)
            {
                Assert.AreEqual(equip.UnlockLevel <= _profile.Level, equip.IsUnlocked, equip.UpgradeId);
            }
        }

    }
}
