using System;
using System.IO;
using Game.Core;
using Game.Network;
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

        [Test(Description = "GET api/user/me로 받은 서버 응답을 스냅샷으로 바꾸고, 서버에 없는 닉네임은 로컬 저장 값을 쓴다")]
        public void GetMe_ReadsServerAndKeepsLocalNickname()
        {
            _transport.Response = new HttpResponseData(200, "{\"gold\":50,\"jem\":3,\"exp\":null,\"energyStored\":20,\"energyUpdatedAt\":\"2026-10-14T01:02:03\",\"stageProgress\":[{\"stageId\":1,\"clearRating\":2,\"claimedRating\":0}],\"upgrades\":[],\"items\":[{\"itemId\":4,\"quantity\":2}],\"codex\":[],\"tutorials\":[]}");

            var snapshot = _api.GetMe().GetAwaiter().GetResult();

            Assert.AreEqual(HttpVerb.Get, _transport.Requests[0].Verb);
            Assert.AreEqual("https://example.test/api/user/me", _transport.Requests[0].Url);
            Assert.AreEqual("로컬닉네임", snapshot.nickname);
            Assert.AreEqual(_store.Load().playerId, snapshot.playerId);
            Assert.AreEqual(50, snapshot.gold);
            Assert.AreEqual(3, snapshot.gems);
            Assert.AreEqual(0, snapshot.exp, "서버가 null이면 0");
            Assert.AreEqual(2, snapshot.stageProgress[0].clearRating);
            Assert.IsEmpty(snapshot.items, "아이템 ID 대응이 정해지기 전이라 반영하지 않는다");
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex("반영하지 않는다"));
        }

        [Test(Description = "서버가 거절하면 ApiException으로 던진다")]
        public void GetMe_ServerRejects_Throws()
        {
            _transport.Response = new HttpResponseData(401, "");

            var ex = Assert.Throws<ApiException>(() => _api.GetMe().GetAwaiter().GetResult());

            Assert.AreEqual("HTTP_401", ex.Code);
        }
    }
}
