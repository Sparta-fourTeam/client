using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;
using Game.Network;
using NUnit.Framework;

namespace Game.Tests
{
    public class HttpApiClientTests
    {
        [Serializable]
        private class Ping
        {
            public string name;
            public int count;
        }

        private FakeHttpTransport _transport;
        private HttpApiClient _client;

        [SetUp]
        public void SetUp()
        {
            _transport = new FakeHttpTransport();
            _client = new HttpApiClient(new HttpApiConfig("https://example.test/", 7f), _transport);
        }

        private static T Run<T>(UniTask<T> task) => task.GetAwaiter().GetResult();

        [Test(Description = "서버 주소 끝의 /와 경로 앞의 /가 겹쳐도 주소가 한 번만 이어진다")]
        public void Get_JoinsBaseUrlAndPath()
        {
            _transport.Response = new HttpResponseData(200, "{\"name\":\"a\",\"count\":1}");

            Run(_client.Get<Ping>("/api/ping"));

            Assert.AreEqual("https://example.test/api/ping", _transport.Requests[0].Url);
            Assert.AreEqual(HttpVerb.Get, _transport.Requests[0].Verb);
            Assert.IsNull(_transport.Requests[0].Body);
            Assert.AreEqual(7f, _transport.LastTimeout);
        }

        [TestCase(400, ApiErrorKind.Rejected)]
        [TestCase(401, ApiErrorKind.Rejected)]
        [TestCase(404, ApiErrorKind.Rejected)]
        [TestCase(409, ApiErrorKind.Rejected)]
        [TestCase(408, ApiErrorKind.Transient)]
        [TestCase(429, ApiErrorKind.Transient)]
        [TestCase(500, ApiErrorKind.Transient)]
        [TestCase(503, ApiErrorKind.Transient)]
        [Test(Description = "HTTP 상태 코드가 거절과 일시적 실패로 나뉘고 코드는 HTTP_상태코드가 된다")]
        public void ErrorStatus_MapsToKindAndCode(int status, ApiErrorKind expected)
        {
            _transport.Response = new HttpResponseData(status, "oops");

            var ex = Assert.Throws<ApiException>(() => Run(_client.Get<Ping>("api/ping")));

            Assert.AreEqual(expected, ex.Kind);
            Assert.AreEqual($"HTTP_{status}", ex.Code);
        }

        [TestCase(400, true)]
        [TestCase(409, true)]
        [TestCase(422, true)]
        [TestCase(401, false)]
        [TestCase(403, false)]
        [TestCase(408, false)]
        [TestCase(429, false)]
        [TestCase(503, false)]
        public void PendingRequest_OnlyDefinitiveRejectionDiscardsOriginalKey(int status, bool discard)
        {
            var error = HttpErrorMapper.Map(new HttpResponseData(status, "{}"));
            Assert.AreEqual(discard, HttpErrorMapper.IsDefinitiveRequestRejection(error));
        }

        [Test(Description = "서버가 오류 본문에 message를 적어 주면 Detail에 담기고, 코드는 그대로 HTTP_상태코드다")]
        public void ErrorBody_MessageGoesToDetail()
        {
            _transport.Response = new HttpResponseData(401, "{\"status\":401,\"message\":\"소셜 로그인 정보가 올바르지 않습니다\"}");

            var ex = Assert.Throws<ApiException>(() => Run(_client.Get<Ping>("api/ping")));

            Assert.AreEqual("HTTP_401", ex.Code);
            Assert.AreEqual("소셜 로그인 정보가 올바르지 않습니다", ex.Detail);
            StringAssert.Contains("올바르지 않습니다", ex.Message);
        }

        [TestCase("oops")]
        [TestCase("{ not json")]
        [TestCase("{\"status\":500}")]
        [Test(Description = "오류 본문이 JSON이 아니거나 message가 없으면 Detail은 없다")]
        public void ErrorBody_WithoutMessage_HasNoDetail(string body)
        {
            _transport.Response = new HttpResponseData(500, body);

            var ex = Assert.Throws<ApiException>(() => Run(_client.Get<Ping>("api/ping")));

            Assert.IsNull(ex.Detail);
        }

        [Test(Description = "제한 시간 안에 응답이 없으면 일시적 실패(TIMEOUT)다")]
        public void Timeout_MapsToTransient()
        {
            _transport.Response = HttpResponseData.Failed(HttpFailure.Timeout);

            var ex = Assert.Throws<ApiException>(() => Run(_client.Get<Ping>("api/ping")));

            Assert.AreEqual(ApiErrorKind.Transient, ex.Kind);
            Assert.AreEqual(HttpErrorMapper.Timeout, ex.Code);
        }

        [Test(Description = "서버에 닿지 못하면 네트워크 실패(NETWORK_ERROR)다")]
        public void NetworkFailure_MapsToNetwork()
        {
            _transport.Response = HttpResponseData.Failed(HttpFailure.Network);

            var ex = Assert.Throws<ApiException>(() => Run(_client.Get<Ping>("api/ping")));

            Assert.AreEqual(ApiErrorKind.Network, ex.Kind);
            Assert.AreEqual(HttpErrorMapper.NetworkError, ex.Code);
        }

        [Test(Description = "취소는 ApiException으로 바뀌지 않고 그대로 전달된다")]
        public void Cancellation_PropagatesAsOperationCanceled()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.Throws<OperationCanceledException>(() => Run(_client.Get<Ping>("api/ping", cts.Token)));
        }

        [Test(Description = "성공 응답의 형식이 계약과 다르면 거절(INVALID_RESPONSE)로 본다")]
        public void MalformedSuccessBody_IsRejected()
        {
            _transport.Response = new HttpResponseData(200, "not json");

            var ex = Assert.Throws<ApiException>(() => Run(_client.Get<Ping>("api/ping")));

            Assert.AreEqual(ApiErrorKind.Rejected, ex.Kind);
            Assert.AreEqual(HttpErrorMapper.InvalidResponse, ex.Code);
        }

        [Test(Description = "본문이 비어 있는데 응답을 기대하면 거절(INVALID_RESPONSE)로 본다")]
        public void EmptyBodyWhenResponseExpected_IsRejected()
        {
            _transport.Response = new HttpResponseData(200, "");

            var ex = Assert.Throws<ApiException>(() => Run(_client.Get<Ping>("api/ping")));

            Assert.AreEqual(HttpErrorMapper.InvalidResponse, ex.Code);
        }

        [Test(Description = "검증 규칙을 주면 JSON 문법만 맞고 필수 필드가 빠진 응답({})을 거절(INVALID_RESPONSE)한다")]
        public void Get_WithValidator_RejectsResponseMissingRequiredField()
        {
            _transport.Response = new HttpResponseData(200, "{}");

            var ex = Assert.Throws<ApiException>(() =>
                Run(_client.Get<Ping>("api/ping", isValid: p => !string.IsNullOrEmpty(p.name))));

            Assert.AreEqual(ApiErrorKind.Rejected, ex.Kind);
            Assert.AreEqual(HttpErrorMapper.InvalidResponse, ex.Code);
        }

        [Test(Description = "POST에도 같은 검증 규칙이 적용된다")]
        public void Post_WithValidator_RejectsResponseMissingRequiredField()
        {
            _transport.Response = new HttpResponseData(200, "{}");

            var ex = Assert.Throws<ApiException>(() =>
                Run(_client.Post<Ping, Ping>("api/ping", new Ping(), isValid: p => !string.IsNullOrEmpty(p.name))));

            Assert.AreEqual(HttpErrorMapper.InvalidResponse, ex.Code);
        }

        [Test(Description = "오류 코드 읽기 지점이 서버가 준 코드를 돌려주면 그 코드를 쓴다")]
        public void ErrorCodeReader_ProvidesDomainCode()
        {
            var client = new HttpApiClient(new HttpApiConfig("https://example.test"), _transport,
                errorCodeReader: (status, body) => body == "bad" ? "INSUFFICIENT_GOLD" : null);
            _transport.Response = new HttpResponseData(400, "bad");

            var ex = Assert.Throws<ApiException>(() => Run(client.Get<Ping>("api/ping")));

            Assert.AreEqual("INSUFFICIENT_GOLD", ex.Code);
            Assert.AreEqual(ApiErrorKind.Rejected, ex.Kind);
        }

        [Test(Description = "오류 코드 읽기 지점이 코드를 못 읽으면 HTTP_상태코드로 대신한다")]
        public void ErrorCodeReader_FallsBackToStatusCode()
        {
            var client = new HttpApiClient(new HttpApiConfig("https://example.test"), _transport,
                errorCodeReader: (status, body) => null);
            _transport.Response = new HttpResponseData(500, "x");

            var ex = Assert.Throws<ApiException>(() => Run(client.Get<Ping>("api/ping")));

            Assert.AreEqual("HTTP_500", ex.Code);
        }
    }
}
