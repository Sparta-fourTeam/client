using System;
using System.IO;
using Game.Core;
using Game.Network;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class HttpPlayerApiTests
    {
        private string _filePath;
        private FakeHttpTransport _transport;
        private HttpPlayerApi _api;
        private LocalSaveStore _store;

        [SetUp]
        public void SetUp()
        {
            _filePath = Path.Combine(Path.GetTempPath(), $"http-player-test-{Guid.NewGuid()}.json");
            _store = new LocalSaveStore(_filePath);
            var save = _store.Load();
            save.nickname = "로컬닉네임";
            _store.Flush(save);
            _transport = new FakeHttpTransport();
            var client = new HttpApiClient(new HttpApiConfig("https://example.test", 7f), _transport);
            _api = new HttpPlayerApi(client, new LocalPlayerApi(_store));
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_filePath)) { File.Delete(_filePath); }
        }

        [Test(Description = "최신 user/me의 튜토리얼·퀘스트를 복원하고 로컬 별명만 유지한다")]
        public void GetMe_ReadsServerAndKeepsLocalNickname()
        {
            _transport.Response = new HttpResponseData(200, HttpAccountProgressTests.State);
            var before = File.ReadAllText(_filePath);
            var snapshot = _api.GetMe().GetAwaiter().GetResult();
            Assert.AreEqual("로컬닉네임", snapshot.nickname);
            Assert.AreEqual(_store.Load().playerId, snapshot.playerId);
            Assert.AreEqual(80, snapshot.gold);
            Assert.AreEqual("intro_battle.dialogue", snapshot.tutorials[0]);
            Assert.AreEqual(13, snapshot.questPeriods[0].points);
            Assert.AreEqual(before, File.ReadAllText(_filePath));
        }

        [Test(Description = "서버가 거절하면 ApiException으로 던진다")]
        public void GetMe_ServerRejects_Throws()
        {
            _transport.Response = new HttpResponseData(401, "");

            var ex = Assert.Throws<ApiException>(() => _api.GetMe().GetAwaiter().GetResult());

            Assert.AreEqual("HTTP_401", ex.Code);
        }

        [TestCase("{}")]
        [TestCase("{\"gold\":12}")]
        [Test(Description = "부분 응답을 빈 상태로 바꿔 계정 진행을 지우지 않는다")]
        public void GetMe_IncompleteStateIsRejected(string json)
        {
            _transport.Response = new HttpResponseData(200, json);
            Assert.AreEqual(HttpErrorMapper.InvalidResponse,
                Assert.Throws<ApiException>(() => _api.GetMe().GetAwaiter().GetResult()).Code);
        }

        [TestCase("tutorials")]
        [TestCase("tutorialStates")]
        [TestCase("quests")]
        public void GetMe_MissingProgressFieldDoesNotEraseExistingAccountState(string field)
        {
            var state = JObject.Parse(HttpAccountProgressTests.State);
            state.Property(field).Remove();
            _transport.Response = new HttpResponseData(200, state.ToString());
            Assert.AreEqual(HttpErrorMapper.InvalidResponse,
                Assert.Throws<ApiException>(() => _api.GetMe().GetAwaiter().GetResult()).Code);
        }
        [TestCase(false, 12)]
        [TestCase(true, 0)]
        public void GetMe_DocumentedLegacyJemFallbackKeepsExplicitGemZero(bool includeGem, int expected)
        {
            var state = JObject.Parse(HttpAccountProgressTests.State);
            state["jem"] = 12;
            if (includeGem) { state["gem"] = 0; }
            else { state.Property("gem").Remove(); }
            _transport.Response = new HttpResponseData(200, state.ToString());
            Assert.AreEqual(expected, _api.GetMe().GetAwaiter().GetResult().gems);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void GetMe_InvalidCurrencyDoesNotUseLegacyFieldToMaskMalformedGem(bool malformedGem)
        {
            var state = JObject.Parse(HttpAccountProgressTests.State);
            if (malformedGem) { state["gem"] = "invalid"; state["jem"] = 12; }
            else { state.Property("gem").Remove(); }
            _transport.Response = new HttpResponseData(200, state.ToString());
            Assert.AreEqual(HttpErrorMapper.InvalidResponse,
                Assert.Throws<ApiException>(() => _api.GetMe().GetAwaiter().GetResult()).Code);
        }
        [TestCase(2, true)]
        [TestCase(-1, false)]
        public void GetMe_LegacyNumericItemsAreSkippedButTheirQuantityIsValidated(int quantity, bool valid)
        {
            var state = JObject.Parse(HttpAccountProgressTests.State);
            state["items"] = JArray.Parse("[{\"itemId\":9,\"quantity\":" + quantity + "},{\"itemId\":\"book.arrow\",\"quantity\":4}]");
            _transport.Response = new HttpResponseData(200, state.ToString());
            if (!valid) { Assert.Throws<ApiException>(() => _api.GetMe().GetAwaiter().GetResult()); return; }
            var snapshot = _api.GetMe().GetAwaiter().GetResult();
            Assert.AreEqual(1, snapshot.items.Count);
            Assert.AreEqual("book.arrow", snapshot.items[0].itemId);
            Assert.AreEqual(4, snapshot.items[0].quantity);
        }

        [TestCase("2026-10-18T20:21:22")]
        [TestCase("2026-10-18T20:21:22.123456")]
        public void GetMe_DocumentedLegacyEnergyUtcTimestampRemainsReadable(string timestamp)
        {
            var state = JObject.Parse(HttpAccountProgressTests.State);
            state["energyUpdatedAt"] = timestamp;
            _transport.Response = new HttpResponseData(200, state.ToString());
            var snapshot = _api.GetMe().GetAwaiter().GetResult();
            Assert.AreEqual(DateTimeKind.Utc, EnergyRule.ParseUpdatedAt(snapshot.energyUpdatedAt).Kind);
        }
    }
}
