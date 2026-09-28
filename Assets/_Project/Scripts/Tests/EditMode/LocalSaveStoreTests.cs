using System;
using System.IO;
using Game.Network;
using NUnit.Framework;

namespace Game.Tests
{
    public class LocalSaveStoreTests
    {
        private string _filePath;

        [SetUp]
        public void CreateTempFilePath()
        {
            _filePath = Path.Combine(Path.GetTempPath(), $"local-save-test-{Guid.NewGuid()}.json");
        }

        [TearDown]
        public void DeleteSaveFile()
        {
            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
            }
        }

        [Test(Description = "저장 파일이 없을 때 Load()는 기본값(스테이지 1, 클리어 안 됨)을 가진 LocalSave를 반환한다")]
        public void Load_WhenNoFileExists_ReturnsDefaultLocalSave()
        {
            var store = new LocalSaveStore(_filePath);

            var save = store.Load();

            Assert.AreEqual(0, save.wallet.gold);
            Assert.AreEqual(1, save.stageProgress.Count);
            Assert.AreEqual(1, save.stageProgress[0].stageId);
            Assert.AreEqual(0, save.stageProgress[0].clearRating);
        }

        [Test(Description = "Flush()로 저장한 지갑/업그레이드 데이터를 Load()가 그대로 복원한다")]
        public void Flush_ThenLoad_RoundTripsData()
        {
            var store = new LocalSaveStore(_filePath);
            var save = new LocalSave { wallet = new WalletRow { gold = 100, energyStored = 5, energyUpdatedAt = "2026-09-28" } };
            save.upgrades.Add(new UpgradeRow { upgradeId = "atk", level = 3 });

            store.Flush(save);
            var loaded = store.Load();

            Assert.AreEqual(100, loaded.wallet.gold);
            Assert.AreEqual(5, loaded.wallet.energyStored);
            Assert.AreEqual("2026-09-28", loaded.wallet.energyUpdatedAt);
            Assert.AreEqual(1, loaded.upgrades.Count);
            Assert.AreEqual("atk", loaded.upgrades[0].upgradeId);
            Assert.AreEqual(3, loaded.upgrades[0].level);
        }

        [Test(Description = "Flush()를 두 번 호출하면 이전 저장 내용이 최신 값으로 덮어써진다")]
        public void Flush_Twice_OverwritesPreviousSave()
        {
            var store = new LocalSaveStore(_filePath);
            store.Flush(new LocalSave { wallet = new WalletRow { gold = 10 } });

            store.Flush(new LocalSave { wallet = new WalletRow { gold = 20 } });
            var loaded = store.Load();

            Assert.AreEqual(20, loaded.wallet.gold);
        }
    }
}
