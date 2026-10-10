using System.Linq;
using Game.Core;
using NUnit.Framework;

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
