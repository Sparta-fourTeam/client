using System;
using System.IO;
using Game.Core;
using Game.Network;
using NUnit.Framework;

namespace Game.Tests
{
    public class LocalUpgradeApiTests
    {
        private string _filePath;
        private LocalSaveStore _store;
        private LocalUpgradeApi _api;
        private GameDataStore _data;

        [SetUp]
        public void SetUp()
        {
            _filePath = Path.Combine(Path.GetTempPath(), $"local-upgrade-test-{Guid.NewGuid()}.json");
            _store = new LocalSaveStore(_filePath);
            _data = new GameDataStore();
            _api = new LocalUpgradeApi(_store, _data);
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
            }
        }

        private void SeedGold(int gold)
        {
            var save = _store.Load();
            save.wallet.gold = gold;
            _store.Flush(save);
        }

        [Test(Description = "골드가 충분하면 레벨을 올리고 비용만큼 차감한다")]
        public void Purchase_WithEnoughGold_LevelsUpAndSpendsGold()
        {
            SeedGold(_data.Upgrades.GetOrThrow("atk").CostAt(1) + 73);

            var snap = _api.Purchase("atk", "key-1").GetAwaiter().GetResult();

            Assert.AreEqual(1, snap.upgrades.Find(u => u.upgradeId == "atk").level);
            Assert.AreEqual(73, snap.gold);

            var second = _api.Purchase("atk", "key-1").GetAwaiter().GetResult();
            Assert.AreEqual(73, second.gold);
            Assert.AreEqual(1, second.upgrades.Find(u => u.upgradeId == "atk").level);
        }

    }
}
