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

        [TestCase(LoginMethod.Guest)]
        [TestCase(LoginMethod.Google)]
        [TestCase(LoginMethod.Apple)]
        public void Login_PreservesExistingNameAndPublicId(LoginMethod method)
        {
            _api.Login(method, "credential").GetAwaiter().GetResult();
            var first = _store.Load();
            Assert.That(first.nickname, Does.StartWith("Nameless_"));
            new LocalAccountApi(_store).ChangeNickname("선택한이름").GetAwaiter().GetResult();
            _api.Login(method, "credential").GetAwaiter().GetResult();
            var restored = _store.Load();
            Assert.That(restored.nickname, Is.EqualTo("선택한이름"));
            Assert.That(restored.playerId, Is.EqualTo(first.playerId));
        }

        [Test(Description = "이름 변경은 프로필과 저장에 반영되고 ID·재화·진행은 유지된다. 닉네임 중복은 허용한다")]
        public void Rename_ChangesOnlyNameAndSurvivesReload()
        {
            var save = _store.Load();
            save.exp = 42;
            save.wallet.gold = 321;
            _store.Flush(save);
            var profile = new PlayerProfile(_data);
            profile.Apply(new LocalPlayerApi(_store).GetMe().GetAwaiter().GetResult());
            var account = new AccountService(new LocalAccountApi(_store), null, null, null, profile);
            account.ChangeNicknameAsync("새이름_2").GetAwaiter().GetResult();
            account.ChangeNicknameAsync("새이름_2").GetAwaiter().GetResult();
            Assert.That(profile.Nickname, Is.EqualTo("새이름_2"));
            Assert.That(profile.PlayerId, Is.EqualTo(save.playerId));
            Assert.That(profile.Exp, Is.EqualTo(42));
            var restored = _store.Load();
            Assert.That(restored.nickname, Is.EqualTo("새이름_2"));
            Assert.That(restored.playerId, Is.EqualTo(save.playerId));
            Assert.That(restored.wallet.gold, Is.EqualTo(321));
        }

        [TestCase("")]
        [TestCase("a")]
        [TestCase("<b>이름</b>")]
        [TestCase("공백 이름")]
        [TestCase("abcdefghijklmnopqrstu")]
        public void Rename_InvalidInput_PreservesSavedName(string input)
        {
            var before = _store.Load();
            var api = new LocalAccountApi(_store);
            var error = Assert.Throws<ApiException>(() => api.ChangeNickname(input).GetAwaiter().GetResult());
            Assert.That(error.Code, Is.EqualTo("INVALID_NICKNAME"));
            Assert.That(_store.Load().nickname, Is.EqualTo(before.nickname));
        }

        [Test(Description = "신규 계정으로 로그인한 직후 바로 StartBattle이 INSUFFICIENT_ENERGY 없이 성공한다")]
        public void Login_AllowsFirstBattleAndPreservesExistingEnergy()
        {
            _api.Login(LoginMethod.Guest, null).GetAwaiter().GetResult();
            Assert.That(_store.Load().wallet.energyStored, Is.EqualTo(_data.Energy.Start));
            var battleApi = new LocalBattleApi(_store, _data);

            var response = battleApi.StartBattle(1).GetAwaiter().GetResult();

            Assert.IsNotEmpty(response.battleId);
            var save = _store.Load();
            save.wallet.energyStored = 3;
            save.wallet.energyUpdatedAt = DateTime.UtcNow.ToString("O");
            _store.Flush(save);
            _api.Login(LoginMethod.Guest, null).GetAwaiter().GetResult();
            Assert.AreEqual(3, _store.Load().wallet.energyStored);
        }

    }
}
