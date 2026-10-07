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
            _catalog = new DummyGrowthCatalog(_profile, _data);
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

        [Test(Description = "Dispose한 뒤에는 프로필이 바뀌어도 스킬 칸의 해금이 갱신되지 않는다")]
        public void Dispose_StopsFollowingProfile()
        {
            var locked = _catalog.Skills.First(s => s.UnlockLevel == 4);
            _catalog.Dispose();

            _profile.Apply(new PlayerSnapshot { exp = ExpFor(5) });

            Assert.IsFalse(locked.IsUnlocked);
        }

        [Test(Description = "장비 레벨과 비용은 Upgrades 테이블과 프로필에서 오고, 강화하면 다음 레벨 비용으로 바뀐다")]
        public void Equips_ReflectUpgradeLevelAndCost()
        {
            var def = _data.Upgrades.GetOrThrow("equipment.hat");
            _profile.Apply(new PlayerSnapshot { exp = ExpFor(5) });
            Assert.AreEqual(0, _catalog.Equips[0].Level);
            Assert.AreEqual(def.CostAt(1), _catalog.Equips[0].CoinCost);
            Assert.AreEqual(def.MaterialAt(1), _catalog.Equips[0].MaterialCost);

            _profile.Apply(new PlayerSnapshot { exp = ExpFor(5), upgrades = new() { new UpgradeRow { upgradeId = "equipment.hat", level = 2 } } });

            Assert.AreEqual(2, _catalog.Equips[0].Level);
            Assert.AreEqual(def.CostAt(3), _catalog.Equips[0].CoinCost);
            Assert.AreEqual(def.MaterialAt(3), _catalog.Equips[0].MaterialCost);
            Assert.AreEqual(def.MaterialItemId, _catalog.Equips[0].MaterialItemId);
        }

        [Test(Description = "최대 레벨의 장비는 다음 비용이 0이고 최대 레벨로 표시된다")]
        public void Equips_AtMaxLevel_HaveNoNextCost()
        {
            var def = _data.Upgrades.GetOrThrow("equipment.hat");
            _profile.Apply(new PlayerSnapshot { exp = ExpFor(5), upgrades = new() { new UpgradeRow { upgradeId = "equipment.hat", level = def.MaxLevel } } });

            var hat = _catalog.Equips[0];

            Assert.IsTrue(hat.IsMaxLevel);
            Assert.AreEqual(0, hat.CoinCost);
            Assert.AreEqual(0, hat.MaterialCost);
        }

        [Test(Description = "장비 효과는 현재 레벨의 효과량과 다음 레벨 증가량을 보여준다 (최대 레벨은 증가량 없음)")]
        public void Equips_EffectTextShowsCurrentAndNext()
        {
            var effect = new UpgradeStatEffect { Kind = "skillDamagePercent", ValuePerLevel = 5 };

            Assert.AreEqual("[기술력] 스킬 대미지 15% ▲ (+5%)", EquipEffectText.Format(effect, 3, false));
            Assert.AreEqual("[기술력] 스킬 대미지 50% ▲", EquipEffectText.Format(effect, 10, true));
            Assert.AreEqual("unknownKind 6 ▲ (+2)", EquipEffectText.Format(new UpgradeStatEffect { Kind = "unknownKind", ValuePerLevel = 2 }, 3, false));
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
                Assert.Greater(skill.MaterialCost, 0, skill.Name);
                Assert.Less(skill.Level, skill.MaxLevel, skill.Name);
            }
        }

        [Test(Description = "강화 데이터가 있는 스킬은 Upgrades 테이블과 프로필에서 레벨·비용·마법북을 가져오고, 강화하면 다음 레벨 비용으로 바뀐다")]
        public void Skills_ReflectUpgradeLevelAndCost()
        {
            var def = _data.Upgrades.GetOrThrow("shuriken");
            var shuriken = _catalog.Skills.First(s => s.Id == "shuriken");
            Assert.AreEqual("shuriken", shuriken.UpgradeId);
            Assert.AreEqual(0, shuriken.Level);
            Assert.AreEqual(def.MaxLevel, shuriken.MaxLevel);
            Assert.AreEqual(def.CostAt(1), shuriken.CoinCost);
            Assert.AreEqual(def.MaterialAt(1), shuriken.MaterialCost);
            Assert.AreEqual(def.MaterialItemId, shuriken.MaterialItemId);

            _profile.Apply(new PlayerSnapshot { upgrades = new() { new UpgradeRow { upgradeId = "shuriken", level = 2 } } });

            Assert.AreEqual(2, shuriken.Level);
            Assert.AreEqual(def.CostAt(3), shuriken.CoinCost);
            Assert.AreEqual(def.MaterialAt(3), shuriken.MaterialCost);
        }

        [Test(Description = "최대 레벨의 스킬은 다음 비용이 0이고 최대 레벨로 표시된다")]
        public void Skills_AtMaxLevel_HaveNoNextCost()
        {
            var def = _data.Upgrades.GetOrThrow("shuriken");
            _profile.Apply(new PlayerSnapshot { upgrades = new() { new UpgradeRow { upgradeId = "shuriken", level = def.MaxLevel } } });

            var shuriken = _catalog.Skills.First(s => s.Id == "shuriken");

            Assert.IsTrue(shuriken.IsMaxLevel);
            Assert.AreEqual(0, shuriken.CoinCost);
            Assert.AreEqual(0, shuriken.MaterialCost);
        }

        [Test(Description = "강화 데이터가 없는 스킬은 업그레이드 ID가 없어 강화할 수 없다")]
        public void Skills_WithoutUpgradeData_HaveNoUpgradeId()
        {
            Assert.IsNull(_catalog.Skills.First(s => s.Id == "frost").UpgradeId);
        }

        [Test(Description = "스킬 강화 재료는 그 스킬 전용 마법북이다 (업그레이드 ID = Skills의 progressionId, 마법북 TargetId = 스킬 ID)")]
        public void SkillUpgrades_UseOwnSkillBook()
        {
            foreach (var skill in _data.LoadSkills().Where(s => !string.IsNullOrEmpty(s.progressionId) && _data.Upgrades.Contains(s.progressionId)))
            {
                var def = _data.Upgrades.GetOrThrow(skill.progressionId);

                Assert.IsTrue(def.UsesMaterial, skill.progressionId);
                Assert.AreEqual(skill.id.ToString(), _data.Items.GetOrThrow(def.MaterialItemId).TargetId, skill.progressionId);
            }
        }

        [Test(Description = "스킬 칸은 화면 칸 수(18)를 넘지 않는다")]
        public void Skills_FitScreen()
        {
            Assert.LessOrEqual(_catalog.Skills.Count, 18);
        }
    }
}
