using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;
using Game.Network;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class HttpBattleSessionApiTests
    {
        private FakeHttpTransport _transport;
        private HttpBattleSessionApi _api;

        [SetUp]
        public void SetUp()
        {
            _transport = new FakeHttpTransport();
            var session = new AuthSession();
            session.Set("access", "refresh");
            _api = new HttpBattleSessionApi(new HttpApiClient(new HttpApiConfig("https://example.test"),
                _transport, tokens: session));
        }

        [Test(Description = "서버가 발급·차감한 뒤 응답이 유실돼도 같은 요청으로 재시도해 같은 전투를 받는다")]
        public void StartBattle_LostResponse_ReusesKeyAndDoesNotChargeAgain()
        {
            var server = new LostStartResponseTransport();
            var api = new HttpBattleSessionApi(new HttpApiClient(new HttpApiConfig("https://example.test"), server));
            var request = new BattleStartRequest();

            var ex = Assert.Throws<ApiException>(() => api.StartBattle(request).GetAwaiter().GetResult());
            Assert.AreEqual(ApiErrorKind.Transient, ex.Kind);
            Assert.AreEqual(1, server.Requests.Count, "자동으로 상태 변경 요청을 재전송하지 않는다");
            var result = api.StartBattle(request).GetAwaiter().GetResult();

            Assert.AreEqual("issued-battle", result.battleId);
            Assert.AreEqual(1, server.Charges);
            Assert.AreEqual(server.Requests[0].Body, server.Requests[1].Body);
            Assert.That(Guid.TryParse(JObject.Parse(server.Requests[0].Body).Value<string>("createId"), out _));

            api.StartBattle(new BattleStartRequest()).GetAwaiter().GetResult();
            Assert.AreEqual(2, server.Charges, "별도 전투는 새 키로 발급한다");
        }

        [Test(Description = "토큰 재발급 뒤에도 동일 createId만 보내고 userId·stageId·seed를 임의로 보내지 않는다")]
        public void StartBattle_Refresh_PreservesBodyAndUsesNewBearer()
        {
            _transport.Queued.Enqueue(new HttpResponseData(401, ""));
            _transport.Queued.Enqueue(new HttpResponseData(200, "{\"accessToken\":\"new-access\",\"refreshToken\":\"new-refresh\"}"));
            _transport.Queued.Enqueue(new HttpResponseData(200, "{\"battleId\":\"battle\"}"));

            var result = _api.StartBattle(new BattleStartRequest()).GetAwaiter().GetResult();

            Assert.AreEqual("battle", result.battleId);
            var sent = _transport.Requests[0];
            var retried = _transport.Requests[2];
            Assert.AreEqual(HttpVerb.Post, sent.Verb);
            Assert.AreEqual("https://example.test/api/battle/start", sent.Url);
            Assert.AreEqual(1, JObject.Parse(sent.Body).Count);
            Assert.AreEqual(sent.Body, retried.Body);
            Assert.AreEqual(sent.Url, retried.Url);
            Assert.AreEqual("Bearer access", sent.Headers["Authorization"]);
            Assert.AreEqual("Bearer new-access", retried.Headers["Authorization"]);
        }

        [TestCase("{}")]
        [TestCase("{\"battleId\":\" \"}")]
        [TestCase("")]
        [Test(Description = "불완전한 성공 응답을 전투 발급 성공으로 처리하지 않는다")]
        public void StartBattle_MissingBattleId_IsRejected(string body)
        {
            _transport.Response = new HttpResponseData(200, body);
            var ex = Assert.Throws<ApiException>(() => _api.StartBattle(new BattleStartRequest()).GetAwaiter().GetResult());
            Assert.AreEqual(HttpErrorMapper.InvalidResponse, ex.Code);
        }

        [Test(Description = "서버의 409는 에너지 부족과 락 충돌을 구분할 코드가 없어 HTTP_409로 유지한다")]
        public void StartBattle_Conflict_IsRejectedWithoutRetry()
        {
            _transport.Response = new HttpResponseData(409, "{\"status\":409,\"error\":\"Conflict\",\"message\":\"해당 유저의 에너지가 부족합니다.\"}");
            var ex = Assert.Throws<ApiException>(() => _api.StartBattle(new BattleStartRequest()).GetAwaiter().GetResult());
            Assert.AreEqual(ApiErrorKind.Rejected, ex.Kind);
            Assert.AreEqual("HTTP_409", ex.Code);
            Assert.AreEqual(1, _transport.Requests.Count);
        }

        [TestCase(false)]
        [TestCase(true)]
        [Test(Description = "PR59 end는 결과 4개 필드, cancel은 battleId만 보내고 둘 다 빈 200을 허용한다")]
        public void Finish_EmptySuccess_UsesCurrentRequestContract(bool cancel)
        {
            _transport.Response = new HttpResponseData(200, "");
            Finish(cancel, "battle").GetAwaiter().GetResult();

            var sent = _transport.Requests[0];
            Assert.AreEqual(HttpVerb.Post, sent.Verb);
            Assert.AreEqual($"https://example.test/api/battle/{(cancel ? "cancel" : "end")}", sent.Url);
            Assert.AreEqual("Bearer access", sent.Headers["Authorization"]);
            var expected = cancel ? "{\"battleId\":\"battle\"}"
                : "{\"battleId\":\"battle\",\"stageId\":7,\"clearedWave\":10,\"rating\":2}";
            Assert.That(JToken.DeepEquals(JObject.Parse(expected), JObject.Parse(sent.Body)));
        }

        [TestCase(0, 0)]
        [TestCase(20, 3)]
        [Test(Description = "명세의 결과 하한·상한을 누락하거나 바꾸지 않고 전송한다")]
        public void EndBattle_ResultBounds_AreSerialized(int clearedWave, int rating)
        {
            _transport.Response = new HttpResponseData(200, "");
            _api.EndBattle(new BattleEndRequest("battle", 7, clearedWave, rating)).GetAwaiter().GetResult();
            var body = JObject.Parse(_transport.Requests[0].Body);
            Assert.AreEqual(clearedWave, body.Value<int>("clearedWave"));
            Assert.AreEqual(rating, body.Value<int>("rating"));
        }

        [TestCase(-1, 0)]
        [TestCase(21, 0)]
        [TestCase(0, -1)]
        [TestCase(0, 4)]
        [Test(Description = "서버 명세 범위를 벗어난 결과로 상태 변경 요청을 보내지 않는다")]
        public void EndBattle_InvalidResult_DoesNotSend(int clearedWave, int rating)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _api.EndBattle(new BattleEndRequest("battle", 7, clearedWave, rating)).GetAwaiter().GetResult());
            Assert.IsEmpty(_transport.Requests);
        }

        [Test(Description = "보상 적용 후 응답 유실 가능성이 있으므로 종료를 자동 재시도하거나 뒤의 404를 성공으로 만들지 않는다")]
        public void EndBattle_LostResponse_ThenNotFound_RemainsUncertain()
        {
            _transport.Queued.Enqueue(HttpResponseData.Failed(HttpFailure.Timeout));
            _transport.Queued.Enqueue(new HttpResponseData(404, ""));
            var request = new BattleEndRequest("battle", 7, 10, 2);

            var first = Assert.Throws<ApiException>(() => _api.EndBattle(request).GetAwaiter().GetResult());
            Assert.AreEqual(ApiErrorKind.Transient, first.Kind);
            Assert.AreEqual(1, _transport.Requests.Count);
            var second = Assert.Throws<ApiException>(() => _api.EndBattle(request).GetAwaiter().GetResult());
            Assert.AreEqual("HTTP_404", second.Code);
            Assert.AreEqual(2, _transport.Requests.Count);
            Assert.AreEqual(_transport.Requests[0].Body, _transport.Requests[1].Body);
        }

        [TestCase(false, 400)]
        [TestCase(false, 404)]
        [TestCase(true, 400)]
        [TestCase(true, 404)]
        [Test(Description = "불일치·미존재를 종료 성공으로 간주하거나 자동 재시도하지 않는다")]
        public void Finish_MismatchOrMissing_IsRejected(bool cancel, int status)
        {
            _transport.Response = new HttpResponseData(status, "");
            var ex = Assert.Throws<ApiException>(() => Finish(cancel, "battle").GetAwaiter().GetResult());
            Assert.AreEqual($"HTTP_{status}", ex.Code);
            Assert.AreEqual(1, _transport.Requests.Count);
        }

        [Test(Description = "요청 전 취소 또는 ID 누락으로 서버 상태를 변경하지 않는다")]
        public void InvalidOrCancelledRequest_DoesNotIssueBattle()
        {
            Assert.Throws<ArgumentNullException>(() => _api.StartBattle(null).GetAwaiter().GetResult());
            Assert.Throws<ArgumentNullException>(() => _api.EndBattle(null).GetAwaiter().GetResult());
            Assert.Throws<ArgumentException>(() => new BattleEndRequest(" ", 7, 0, 0));
            Assert.Throws<ArgumentException>(() => _api.CancelBattle(null).GetAwaiter().GetResult());
            Assert.IsEmpty(_transport.Requests);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            Assert.Throws<OperationCanceledException>(() => _api.StartBattle(new BattleStartRequest(), cancellation.Token).GetAwaiter().GetResult());
        }

        private UniTask Finish(bool cancel, string battleId) => cancel ? _api.CancelBattle(battleId)
            : _api.EndBattle(new BattleEndRequest(battleId, 7, 10, 2));

        // PR #57의 동일 키 재발급 계약만 재현한다. 실제 서버·DB 동시성 검증은 아니다.
        private sealed class LostStartResponseTransport : IHttpTransport
        {
            public readonly List<HttpRequestData> Requests = new();
            public int Charges;
            private string _createId;

            public UniTask<HttpResponseData> Send(HttpRequestData request, float timeoutSeconds, CancellationToken cancellationToken)
            {
                Requests.Add(request);
                string key = JObject.Parse(request.Body).Value<string>("createId");
                if (_createId != key)
                {
                    _createId = key;
                    Charges++;
                }

                return UniTask.FromResult(Requests.Count == 1
                    ? HttpResponseData.Failed(HttpFailure.Timeout)
                    : new HttpResponseData(200, "{\"battleId\":\"issued-battle\"}"));
            }
        }
    }
}
