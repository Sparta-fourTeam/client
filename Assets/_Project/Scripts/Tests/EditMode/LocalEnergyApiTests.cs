using System;
using System.IO;
using Game.Core;
using Game.Network;
using NUnit.Framework;

namespace Game.Tests
{
    public class LocalEnergyApiTests
    {
        private string _filePath;
        private LocalSaveStore _store;
        private GameDataStore _data;
        private LocalEnergyApi _api;

        private EnergyConfig Config => _data.Energy;

        [SetUp]
        public void SetUp()
        {
            _filePath = Path.Combine(Path.GetTempPath(), $"local-energy-test-{Guid.NewGuid()}.json");
            _store = new LocalSaveStore(_filePath);
            _data = new GameDataStore();
            _api = new LocalEnergyApi(_store, _data);
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
            }
        }

        // 방금 갱신된 상태로 저장해 두어 자연 회복이 끼어들지 않게 한다
        private void SaveEnergy(int stored)
        {
            var save = _store.Load();
            save.wallet.energyStored = stored;
            save.wallet.energyUpdatedAt = DateTime.UtcNow.ToString("O");
            _store.Flush(save);
        }

        [Test(Description = "자연 회복 경과분을 반영하고 갱신 시각을 최신화한다")]
        public void Recover_Natural_AppliesElapsedRegenAndUpdatesTimestamp()
        {
            var save = _store.Load();
            save.wallet.energyStored = 0;
            save.wallet.energyUpdatedAt = DateTime.UtcNow.AddSeconds(-Config.RegenSeconds * 2).ToString("O");
            _store.Flush(save);

            var snap = _api.Recover(EnergySource.Natural).GetAwaiter().GetResult();

            Assert.AreEqual(Math.Min(2, Config.Max), snap.energyStored);
        }

        [TestCase(EnergySource.Natural)]
        [TestCase(EnergySource.Ad)]
        [TestCase(EnergySource.Purchase)]
        public void Recover_BelowCap_PreservesPartialRegenAcrossReload(EnergySource source)
        {
            var save = _store.Load();
            var updatedAt = DateTime.UtcNow.AddSeconds(-Config.RegenSeconds * 1.5);
            save.wallet.energyStored = 0;
            save.wallet.energyUpdatedAt = updatedAt.ToString("O");
            _store.Flush(save);

            _api.Recover(source, "partial").GetAwaiter().GetResult();

            var reloaded = new LocalSaveStore(_filePath).Load();
            Assert.That(EnergyRule.ParseUpdatedAt(reloaded.wallet.energyUpdatedAt),
                Is.EqualTo(updatedAt.AddSeconds(Config.RegenSeconds)));
        }

        [Test]
        public void Recover_Ad_ReplayedKeyAfterReloadDoesNotGrantAgainAndDifferentKeyDoes()
        {
            SaveEnergy(2);
            var save = _store.Load();
            save.ledger.Add(new LedgerRow { sourceType = "UPGRADE", idempotencyKey = "same" });
            _store.Flush(save);
            _api.Recover(EnergySource.Ad, "same").GetAwaiter().GetResult();
            var replayApi = new LocalEnergyApi(new LocalSaveStore(_filePath), _data);
            var replay = replayApi.Recover(EnergySource.Ad, "same").GetAwaiter().GetResult();
            Assert.AreEqual(2 + Config.AdRecoverAmount, replay.energyStored);
            var next = replayApi.Recover(EnergySource.Ad, "different").GetAwaiter().GetResult();
            Assert.AreEqual(2 + 2 * Config.AdRecoverAmount, next.energyStored);
            Assert.AreEqual(2, _store.Load().ledger.FindAll(row => row.sourceType == "ENERGY_AD").Count);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void Recover_Ad_RequiresKeyWithoutChangingSave(string key)
        {
            SaveEnergy(2);
            string before = File.ReadAllText(_filePath);
            Assert.Throws<ApiException>(() => _api.Recover(EnergySource.Ad, key).GetAwaiter().GetResult());
            Assert.AreEqual(before, File.ReadAllText(_filePath));
        }

        [Test(Description = "구매 회복은 PurchaseRecoverAmount만큼 더한다 (보석 차감은 서버 연동 전까지 생략)")]
        public void Recover_Purchase_AddsPurchaseAmount()
        {
            SaveEnergy(2);

            var snap = _api.Recover(EnergySource.Purchase).GetAwaiter().GetResult();

            Assert.AreEqual(2 + Config.PurchaseRecoverAmount, snap.energyStored);
        }

        [Test(Description = "회복 결과는 세이브 파일에 저장되어 다시 불러와도 유지된다")]
        public void Recover_Ad_PersistsToSave()
        {
            SaveEnergy(2);

            _api.Recover(EnergySource.Ad, "key").GetAwaiter().GetResult();

            Assert.AreEqual(2 + Config.AdRecoverAmount, _store.Load().wallet.energyStored);
        }

        [Test(Description = "회복 전에 쌓인 자연 회복분을 먼저 반영한 뒤 보상을 더한다")]
        public void Recover_Ad_AppliesElapsedRegenBeforeReward()
        {
            var save = _store.Load();
            save.wallet.energyStored = 0;
            save.wallet.energyUpdatedAt = DateTime.UtcNow.AddSeconds(-Config.RegenSeconds * 2).ToString("O"); // 2회복
            _store.Flush(save);

            var snap = _api.Recover(EnergySource.Ad, "key").GetAwaiter().GetResult();

            Assert.AreEqual(2 + Config.AdRecoverAmount, snap.energyStored);
        }
    }
}
