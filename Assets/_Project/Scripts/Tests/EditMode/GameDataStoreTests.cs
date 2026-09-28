using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    public class GameDataStoreTests
    {
        [Test(Description = "MockData의 몬스터/스테이지/업그레이드/에너지 테이블을 읽어온다")]
        public void Constructor_LoadsAllTables()
        {
            var store = new GameDataStore();

            Assert.AreEqual(30, store.Monsters.GetOrThrow(1).Hp);
            Assert.IsTrue(store.Monsters.GetOrThrow(100).IsBoss);
            Assert.AreEqual(100, store.Stages.GetOrThrow(1).ClearGold);
            Assert.AreEqual(3, store.Upgrades.GetOrThrow("atk").MaxLevel);
            Assert.AreEqual(5, store.Energy.Cost);
        }

        [Test(Description = "UpgradeDefinition.CostAt은 1부터 시작하는 레벨로 LevelCosts를 찾는다")]
        public void UpgradeDefinition_CostAt_UsesOneBasedLevel()
        {
            var store = new GameDataStore();

            Assert.AreEqual(100, store.Upgrades.GetOrThrow("atk").CostAt(1));
            Assert.AreEqual(500, store.Upgrades.GetOrThrow("atk").CostAt(3));
        }
    }
}
