using System;
using System.IO;
using Game.Core;
using Game.Network;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>게스트 계정에 구글 계정을 붙이는 서버 API (POST /api/auth/social/link)</summary>
    public sealed class ServerBackedAccountApiTests
    {
        private string _filePath;
        private LocalSaveStore _store;
        private FakeHttpTransport _transport;
        private ServerBackedAccountApi _api;

        [SetUp]
        public void SetUp()
        {
            _filePath = Path.Combine(Path.GetTempPath(), $"account-link-test-{Guid.NewGuid()}.json");
            _store = new LocalSaveStore(_filePath);
            _transport = new FakeHttpTransport { Response = new HttpResponseData(204, string.Empty) };
            var session = new AuthSession();
            session.Set("guest-access", "guest-refresh");
            var client = new HttpApiClient(new HttpApiConfig("https://example.test", 7f), _transport, session);
            _api = new ServerBackedAccountApi(client, new LocalAccountApi(_store));
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_filePath)) { File.Delete(_filePath); }
        }

        [Test(Description = "구글 ID 토큰을 게스트 토큰과 함께 POST /api/auth/social/link로 보내고, 204면 성공한다")]
        public void Link_SendsProviderAndIdTokenWithBearer()
        {
            _api.LinkAccount(LoginMethod.Google, "google-id-token").GetAwaiter().GetResult();

            var request = _transport.Requests[0];
            Assert.AreEqual(HttpVerb.Post, request.Verb);
            Assert.AreEqual("https://example.test/api/auth/social/link", request.Url);
            StringAssert.Contains("\"provider\":\"GOOGLE\"", request.Body);
            StringAssert.Contains("\"idToken\":\"google-id-token\"", request.Body);
            Assert.AreEqual("Bearer guest-access", request.Headers["Authorization"]);
        }

        [TestCase(409)]
        [TestCase(401)]
        [Test(Description = "서버가 거절하면 ApiException으로 던진다 (409: 이미 연동된 계정, 401: 로그인 필요)")]
        public void Link_ServerRejects_Throws(int status)
        {
            _transport.Response = new HttpResponseData(status, "{\"message\":\"x\"}");

            var ex = Assert.Throws<ApiException>(() => _api.LinkAccount(LoginMethod.Google, "t").GetAwaiter().GetResult());

            Assert.AreEqual($"HTTP_{status}", ex.Code);
            Assert.AreEqual(ApiErrorKind.Rejected, ex.Kind);
        }

        [Test(Description = "게스트는 연동할 외부 계정이 아니므로 서버에 보내지 않는다")]
        public void Link_Guest_IsRejectedBeforeSending()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _api.LinkAccount(LoginMethod.Guest, "g").GetAwaiter().GetResult());
            Assert.AreEqual(0, _transport.Requests.Count);
        }

        [Test(Description = "계정 삭제는 서버 API가 없어 로컬 저장을 지운다")]
        public void Delete_UsesLocalStore()
        {
            _store.Load();
            _store.Flush(_store.Load());
            Assert.IsTrue(_store.Exists());

            _api.DeleteAccount().GetAwaiter().GetResult();

            Assert.IsFalse(_store.Exists());
            Assert.AreEqual(0, _transport.Requests.Count);
        }
    }
}
