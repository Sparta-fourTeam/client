using Game.Core;
using Game.Network;
using NUnit.Framework;

namespace Game.Tests
{
    public class HttpDataVersionApiTests
    {
        private FakeHttpTransport _transport;
        private HttpDataVersionApi _api;

        [SetUp]
        public void SetUp()
        {
            _transport = new FakeHttpTransport();
            var client = new HttpApiClient(new HttpApiConfig("https://example.test"), _transport);
            _api = new HttpDataVersionApi(client);
        }

        [Test(Description = "GET /api/data/version을 호출한다")]
        public void GetVersions_CallsVersionEndpoint()
        {
            _transport.Response = new HttpResponseData(200, "{\"revision\":3,\"tables\":{\"Skills\":2}}");

            _api.GetVersions().GetAwaiter().GetResult();

            Assert.AreEqual("https://example.test/api/data/version", _transport.Requests[0].Url);
            Assert.AreEqual(HttpVerb.Get, _transport.Requests[0].Verb);
        }

        [Test(Description = "서버의 테이블별 리비전이 DataVersions.revisions로 옮겨진다")]
        public void GetVersions_MapsTablesToRevisions()
        {
            _transport.Response = new HttpResponseData(200, "{\"revision\":5,\"tables\":{\"Skills\":2,\"Stages\":4}}");

            var versions = _api.GetVersions().GetAwaiter().GetResult();

            Assert.AreEqual(2, versions.revisions.Count);
            Assert.AreEqual(2, versions.revisions["Skills"]);
            Assert.AreEqual(4, versions.revisions["Stages"]);
        }

        [Test(Description = "테이블이 하나도 없는 응답은 빈 리비전 목록이 된다")]
        public void GetVersions_EmptyTables_ReturnsEmptyRevisions()
        {
            _transport.Response = new HttpResponseData(200, "{\"revision\":1,\"tables\":{}}");

            var versions = _api.GetVersions().GetAwaiter().GetResult();

            Assert.AreEqual(0, versions.revisions.Count);
        }

        [Test(Description = "tables가 빠진 응답은 계약과 다른 응답으로 거절한다")]
        public void GetVersions_MissingTables_IsRejected()
        {
            _transport.Response = new HttpResponseData(200, "{\"revision\":1}");

            var ex = Assert.Throws<ApiException>(() => _api.GetVersions().GetAwaiter().GetResult());

            Assert.AreEqual(ApiErrorKind.Rejected, ex.Kind);
            Assert.AreEqual(HttpErrorMapper.InvalidResponse, ex.Code);
        }
    }
}
