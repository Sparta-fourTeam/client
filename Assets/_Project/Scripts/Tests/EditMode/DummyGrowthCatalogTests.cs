using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>하드코딩 데이터가 화면이 기대하는 모양을 지키는지 확인한다 (실제 구현으로 바꿀 때도 같은 규칙을 지켜야 한다)</summary>
    public sealed class DummyGrowthCatalogTests
    {
        private DummyGrowthCatalog _catalog;

        [SetUp]
        public void SetUp()
        {
            _catalog = new DummyGrowthCatalog();
        }

        [Test(Description = "장비 칸은 캐릭터 화면 칸 수(오른쪽 6 + 왼쪽 1)와 같고, 칸 번호가 순서대로다")]
        public void Equips_MatchScreenSlots()
        {
            Assert.AreEqual(7, _catalog.Equips.Count);
            for (int i = 0; i < _catalog.Equips.Count; i++)
            {
                Assert.AreEqual(i, _catalog.Equips[i].Slot);
            }
        }

        [Test(Description = "장비 칸은 캐릭터 레벨에 맞춰 열린다")]
        public void Equips_UnlockByCharacterLevel()
        {
            foreach (EquipInfo equip in _catalog.Equips)
            {
                if (equip.UnlockLevel <= _catalog.Character.Level)
                {
                    Assert.IsTrue(equip.IsUnlocked, equip.Name);
                }
            }
        }

        [Test(Description = "장식품 효과는 팝업 줄 수(5)를 넘지 않는다")]
        public void Equips_EffectsFitPopup()
        {
            foreach (EquipInfo equip in _catalog.Equips)
            {
                Assert.LessOrEqual(equip.Effects.Count, 5, equip.Name);
            }
        }

        [Test(Description = "열린 스킬은 강화 전후 능력치와 비용이 있고, 능력치 줄은 팝업 줄 수(3)를 넘지 않는다")]
        public void UnlockedSkills_HaveStatsAndCost()
        {
            foreach (SkillInfo skill in _catalog.Skills)
            {
                if (!skill.IsUnlocked)
                {
                    continue;
                }

                Assert.That(skill.Stats.Count, Is.InRange(1, 3), skill.Name);
                Assert.Greater(skill.CoinCost, 0, skill.Name);
                Assert.Greater(skill.BookCost, 0, skill.Name);
                Assert.Less(skill.Level, skill.MaxLevel, skill.Name);
            }
        }

        [Test(Description = "스킬 칸은 화면 칸 수(18)를 넘지 않는다")]
        public void Skills_FitScreen()
        {
            Assert.LessOrEqual(_catalog.Skills.Count, 18);
        }
    }
}
