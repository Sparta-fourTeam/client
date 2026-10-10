using System;
using System.IO;
using Game.Core;
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

            save.wallet.gold = 20;
            save.upgrades.Clear();
            store.Flush(save);
            var overwritten = new LocalSaveStore(_filePath).Load();
            Assert.AreEqual(20, overwritten.wallet.gold);
            Assert.IsEmpty(overwritten.upgrades);
        }

        [Test(Description = "Load()를 여러 번 반복해도 stageProgress의 기본 시드 행이 중복되지 않는다 " +
            "(필드 기본값이 있는 리스트를 Newtonsoft가 이어붙이는 문제 회귀 테스트)")]
        public void Load_CalledRepeatedly_DoesNotDuplicateSeededStageProgress()
        {
            var store = new LocalSaveStore(_filePath);
            store.Flush(store.Load());

            store.Load();
            store.Load();
            var loaded = store.Load();

            Assert.AreEqual(1, loaded.stageProgress.Count);
            loaded.stageProgress[0].clearRating = 3;
            store.Flush(loaded);
            var restored = new LocalSaveStore(_filePath).Load();
            Assert.AreEqual(1, restored.stageProgress.Count);
            Assert.AreEqual(3, restored.stageProgress[0].clearRating);
        }

    }
}
