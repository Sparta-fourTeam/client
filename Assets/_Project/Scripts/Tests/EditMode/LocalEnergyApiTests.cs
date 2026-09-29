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
        private LocalEnergyApi _api;

        [SetUp]
        public void SetUp()
        {
            _filePath = Path.Combine(Path.GetTempPath(), $"local-energy-test-{Guid.NewGuid()}.json");
            _store = new LocalSaveStore(_filePath);
            _api = new LocalEnergyApi(_store, new GameDataStore());
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
            }
        }

        [Test(Description = "자연 회복 경과분을 반영하고 갱신 시각을 최신화한다")]
        public void Recover_Natural_AppliesElapsedRegenAndUpdatesTimestamp()
        {
            var save = _store.Load();
            save.wallet.energyStored = 0;
            save.wallet.energyUpdatedAt = DateTime.UtcNow.AddSeconds(-600).ToString("O"); // RegenSeconds=300 -> 2회복
            _store.Flush(save);

            var snap = _api.Recover(EnergySource.Natural).GetAwaiter().GetResult();

            Assert.AreEqual(2, snap.energyStored);
        }

        [Test(Description = "광고 회복은 보상량이 정해지지 않아 아직 거절한다(MVP 범위 밖)")]
        public void Recover_Ad_ThrowsNotSupported()
        {
            var ex = Assert.Throws<ApiException>(() => _api.Recover(EnergySource.Ad, "key").GetAwaiter().GetResult());

            Assert.AreEqual("AD_ENERGY_NOT_SUPPORTED", ex.Code);
        }
    }
}
