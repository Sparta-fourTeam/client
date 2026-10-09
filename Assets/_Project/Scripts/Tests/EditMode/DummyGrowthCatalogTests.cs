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
    }
}
