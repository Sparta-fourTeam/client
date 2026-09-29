using System;
using System.IO;
using Game.Core;
using Game.Network;
using NUnit.Framework;

namespace Game.Tests
{
    public class LocalAuthApiTests
    {
        private string _filePath;
        private LocalSaveStore _store;
        private GameDataStore _data;
        private LocalAuthApi _api;

        [SetUp]
        public void SetUp()
        {
            _filePath = Path.Combine(Path.GetTempPath(), $"local-auth-test-{Guid.NewGuid()}.json");
            _store = new LocalSaveStore(_filePath);
            _data = new GameDataStore();
            _api = new LocalAuthApi(_store, _data);
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
            }
        }

        [Test(Description = "신규 계정으로 로그인하면 에너지를 최대치로 채우고 갱신 시각을 기록한다")]
        public void Login_NewAccount_FillsEnergyAndSetsUpdatedAt()
        {
            _api.Login().GetAwaiter().GetResult();

            var save = _store.Load();
            Assert.AreEqual(_data.Energy.Max, save.wallet.energyStored);
            Assert.IsFalse(string.IsNullOrEmpty(save.wallet.energyUpdatedAt));
        }

        [Test(Description = "신규 계정으로 로그인한 직후 바로 StartBattle이 INSUFFICIENT_ENERGY 없이 성공한다")]
        public void Login_NewAccount_ThenStartBattle_Succeeds()
        {
            _api.Login().GetAwaiter().GetResult();
            var battleApi = new LocalBattleApi(_store, _data);

            var response = battleApi.StartBattle(1, 1).GetAwaiter().GetResult();

            Assert.IsNotEmpty(response.battleId);
        }

        [Test(Description = "기존 계정으로 다시 로그인해도 에너지 값을 덮어쓰지 않는다")]
        public void Login_ExistingAccount_KeepsEnergy()
        {
            var save = _store.Load();
            save.wallet.energyStored = 3;
            save.wallet.energyUpdatedAt = DateTime.UtcNow.AddMinutes(-5).ToString("O");
            _store.Flush(save);

            _api.Login().GetAwaiter().GetResult();

            Assert.AreEqual(3, _store.Load().wallet.energyStored);
        }
    }
}
