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

        [SetUp]
        public void SetUp()
        {
            _filePath = Path.Combine(Path.GetTempPath(), $"local-upgrade-test-{Guid.NewGuid()}.json");
            _store = new LocalSaveStore(_filePath);
            _api = new LocalUpgradeApi(_store, new GameDataStore());
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
            SeedGold(200);

            var snap = _api.Purchase("atk", "key-1").GetAwaiter().GetResult();

            Assert.AreEqual(1, snap.upgrades.Find(u => u.upgradeId == "atk").level);
            Assert.AreEqual(100, snap.gold); // CostAt(1) = 100
        }

        [Test(Description = "골드가 부족하면 INSUFFICIENT_GOLD로 거절한다")]
        public void Purchase_WithoutEnoughGold_ThrowsInsufficientGold()
        {
            SeedGold(50);

            var ex = Assert.Throws<ApiException>(() => _api.Purchase("atk", "key-1").GetAwaiter().GetResult());

            Assert.AreEqual("INSUFFICIENT_GOLD", ex.Code);
        }

        [Test(Description = "최대 레벨에서 구매하면 ALREADY_MAX로 거절한다")]
        public void Purchase_AtMaxLevel_ThrowsAlreadyMax()
        {
            SeedGold(10_000);
            _api.Purchase("atk", "key-1").GetAwaiter().GetResult();
            _api.Purchase("atk", "key-2").GetAwaiter().GetResult();
            _api.Purchase("atk", "key-3").GetAwaiter().GetResult(); // MaxLevel = 3

            var ex = Assert.Throws<ApiException>(() => _api.Purchase("atk", "key-4").GetAwaiter().GetResult());

            Assert.AreEqual("ALREADY_MAX", ex.Code);
        }

        [Test(Description = "같은 idempotencyKey로 연타해도 한 번만 차감된다")]
        public void Purchase_SameIdempotencyKeyTwice_ChargesOnce()
        {
            SeedGold(200);
            _api.Purchase("atk", "same-key").GetAwaiter().GetResult();

            var second = _api.Purchase("atk", "same-key").GetAwaiter().GetResult();

            Assert.AreEqual(100, second.gold);
            Assert.AreEqual(1, second.upgrades.Find(u => u.upgradeId == "atk").level);
        }
    }
}
