using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;
using Game.Network;
using NUnit.Framework;

namespace Game.Tests
{
    public class HttpApiClientTests
    {
        private sealed class FakeTransport : IHttpTransport
        {
            public HttpResponseData Response = new HttpResponseData(200, "{}");
            public Exception ToThrow;
            public readonly List<HttpRequestData> Requests = new();
            public float LastTimeout;

            public UniTask<HttpResponseData> Send(HttpRequestData request, float timeoutSeconds, CancellationToken cancellationToken)
            {
                Requests.Add(request);
                LastTimeout = timeoutSeconds;
                cancellationToken.ThrowIfCancellationRequested();
                if (ToThrow != null)
                {
                    throw ToThrow;
                }

                return UniTask.FromResult(Response);
            }
        }

        private sealed class FixedHeaders : IAuthHeaderProvider
        {
            public IReadOnlyDictionary<string, string> GetHeaders() =>
                new Dictionary<string, string> { ["Authorization"] = "Bearer test-token" };
        }

        [Serializable]
        private class Ping
        {
            public string name;
            public int count;
        }

        private FakeTransport _transport;
        private HttpApiClient _client;

        [SetUp]
        public void SetUp()
        {
            _transport = new FakeTransport();
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

        [Test(Description = "정상 응답 본문이 DTO로 변환된다")]
        public void Get_DeserializesBody()
        {
            _transport.Response = new HttpResponseData(200, "{\"name\":\"nova\",\"count\":3}");

            var result = Run(_client.Get<Ping>("api/ping"));

            Assert.AreEqual("nova", result.name);
            Assert.AreEqual(3, result.count);
        }

        [Test(Description = "POST는 요청 DTO를 JSON 본문으로 보낸다")]
        public void Post_SendsJsonBody()
        {
            _transport.Response = new HttpResponseData(200, "{\"name\":\"ok\",\"count\":0}");

            Run(_client.Post<Ping, Ping>("api/ping", new Ping { name = "x", count = 2 }));

            var request = _transport.Requests[0];
            Assert.AreEqual(HttpVerb.Post, request.Verb);
            StringAssert.Contains("\"name\":\"x\"", request.Body);
            StringAssert.Contains("\"count\":2", request.Body);
        }

        [Test(Description = "응답 본문이 없는 POST는 2xx이면 성공한다")]
        public void PostWithoutResponse_SucceedsOn200WithEmptyBody()
        {
            _transport.Response = new HttpResponseData(200, "");

            Assert.DoesNotThrow(() => _client.Post("api/ping", new Ping()).GetAwaiter().GetResult());
        }

        [Test(Description = "인증 헤더 제공자가 돌려준 헤더가 요청에 붙는다")]
        public void Request_CarriesAuthHeaders()
        {
            var client = new HttpApiClient(new HttpApiConfig("https://example.test"), _transport, new FixedHeaders());
            _transport.Response = new HttpResponseData(200, "{\"name\":\"a\",\"count\":1}");

            Run(client.Get<Ping>("api/ping"));

            Assert.AreEqual("Bearer test-token", _transport.Requests[0].Headers["Authorization"]);
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

        [Test(Description = "실패해도 자동으로 다시 보내지 않는다 (상태 변경 요청의 중복 처리 방지)")]
        public void Failure_DoesNotRetryAutomatically()
        {
            _transport.Response = new HttpResponseData(503, "");

            Assert.Throws<ApiException>(() => Run(_client.Post<Ping, Ping>("api/ping", new Ping())));

            Assert.AreEqual(1, _transport.Requests.Count);
        }

        [Test(Description = "서버 주소가 비어 있거나 제한 시간이 0 이하이면 만들 수 없다")]
        public void Config_RejectsInvalidValues()
        {
            Assert.Throws<ArgumentException>(() => new HttpApiConfig(" "));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HttpApiConfig("https://example.test", 0f));
        }
    }
}
