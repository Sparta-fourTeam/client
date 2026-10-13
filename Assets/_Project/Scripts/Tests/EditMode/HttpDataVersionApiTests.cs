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

        [Test(Description = "버전 endpoint의 정상 응답에서 테이블 버전을 복원한다")]
        public void GetVersions_CallsVersionEndpoint()
        {
            _transport.Response = new HttpResponseData(200, "{\"revision\":3,\"tables\":{\"Skills\":2}}");

            var versions = _api.GetVersions().GetAwaiter().GetResult();

            Assert.AreEqual("https://example.test/api/data/version", _transport.Requests[0].Url);
            Assert.AreEqual(HttpVerb.Get, _transport.Requests[0].Verb);
            Assert.AreEqual(2, versions.revisions["Skills"]);
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
