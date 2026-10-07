using System.Linq;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>하드코딩 데이터가 화면이 기대하는 모양을 지키는지 확인한다 (실제 구현으로 바꿀 때도 같은 규칙을 지켜야 한다)</summary>
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
            _catalog = new DummyGrowthCatalog(_profile);
        }

        // 레벨 level에 막 도달하는 누적 경험치
        private int ExpFor(int level)
        {
            int exp = 0;
            for (int l = 1; l < level; l++) { exp += _data.PlayerLevels.RequiredToNext(l); }
            return exp;
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

        [Test(Description = "장비 칸은 플레이어 레벨에 맞춰 열린다")]
        public void Equips_UnlockByPlayerLevel()
        {
            _profile.Apply(new PlayerSnapshot { exp = ExpFor(5) });

            foreach (EquipInfo equip in _catalog.Equips)
            {
                if (equip.UnlockLevel <= _profile.Level)
                {
                    Assert.IsTrue(equip.IsUnlocked, equip.Name);
                }
            }
        }

        [Test(Description = "장비·스킬 칸의 해금 여부가 프로필 레벨에 맞춰 다시 계산된다 (레벨 1: 잠김, 5: 해당 레벨까지 열림)")]
        public void Unlocks_RecomputeWhenProfileChanges()
        {
            // 레벨 1: 마지막 장비 칸과 처음부터 열린 스킬만 열려 있다
            Assert.IsFalse(_catalog.Equips[0].IsUnlocked);
            Assert.IsTrue(_catalog.Equips[_catalog.Equips.Count - 1].IsUnlocked);
            Assert.IsFalse(_catalog.Skills.First(s => s.UnlockLevel == 4).IsUnlocked);
            Assert.IsTrue(_catalog.Skills.First(s => s.UnlockLevel == 1).IsUnlocked);

            _profile.Apply(new PlayerSnapshot { exp = ExpFor(5) });

            Assert.IsTrue(_catalog.Equips[0].IsUnlocked);           // 해금 레벨 2
            Assert.IsFalse(_catalog.Equips[4].IsUnlocked);          // 해금 레벨 6
            Assert.IsTrue(_catalog.Skills.First(s => s.UnlockLevel == 4).IsUnlocked);
            Assert.IsTrue(_catalog.Skills.First(s => s.UnlockLevel == 5).IsUnlocked);
            Assert.IsFalse(_catalog.Skills.First(s => s.UnlockLevel == 6).IsUnlocked);

            // 레벨이 내려가면(다른 스냅샷으로 교체) 다시 잠긴다
            _profile.Apply(new PlayerSnapshot { exp = 0 });

            Assert.IsFalse(_catalog.Equips[0].IsUnlocked);
            Assert.IsFalse(_catalog.Skills.First(s => s.UnlockLevel == 4).IsUnlocked);
        }

        [Test(Description = "Dispose한 뒤에는 프로필이 바뀌어도 카탈로그가 갱신되지 않는다")]
        public void Dispose_StopsFollowingProfile()
        {
            _catalog.Dispose();

            _profile.Apply(new PlayerSnapshot { exp = ExpFor(5) });

            Assert.IsFalse(_catalog.Equips[0].IsUnlocked);
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
