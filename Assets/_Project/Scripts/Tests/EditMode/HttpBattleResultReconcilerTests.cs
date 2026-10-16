using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;
using Game.Network;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class HttpBattleResultReconcilerTests
    {
        private const string State = "{\"gold\":80,\"gem\":3,\"exp\":20,\"energyStored\":15,"
            + "\"energyUpdatedAt\":\"2026-10-16T23:00:00\",\"stageProgress\":[{\"stageId\":7,\"clearRating\":3,\"claimedRating\":0}],"
            + "\"items\":[{\"itemId\":\"book.arrow\",\"quantity\":2}],\"upgrades\":[],\"codex\":[],\"tutorials\":[]}";
        private FakeHttpTransport _transport;
        private AuthSession _tokens;
        private HttpBattleResultReconciler _reconciler;

        [SetUp]
        public void SetUp()
        {
            _transport = new FakeHttpTransport { Response = new HttpResponseData(200, State) };
            _tokens = new AuthSession();
            _tokens.Set("access-a", "refresh-a");
            _reconciler = Create(_transport, _tokens);
        }

        [Test(Description = "end 확인 후 서버 상태를 읽되 Local 잔액·진척 파일을 쓰거나 보상 차액을 계산하지 않는다")]
        public void Submit_Acknowledged_ReadsServerState_WithoutLocalMutation()
        {
            var path = Path.Combine(Path.GetTempPath(), $"battle-reconcile-{Guid.NewGuid():N}.json");
            try
            {
                var store = new LocalSaveStore(path);
                store.Flush(store.Load());
                var before = File.ReadAllBytes(path);
                _transport.Queued.Enqueue(new HttpResponseData(200, ""));

                var result = _reconciler.Submit().GetAwaiter().GetResult();

                Assert.AreEqual(BattleSubmissionStatus.Acknowledged, result.SubmissionStatus);
                Assert.AreEqual(80, result.ObservedState.gold);
                Assert.AreEqual("2026-10-16T23:00:00", result.ObservedState.energyUpdatedAt);
                Assert.AreEqual(2, result.ObservedState.items.Single().quantity);
                Assert.AreEqual(3, result.ObservedState.stageProgress.Single().clearRating);
                CollectionAssert.AreEqual(before, File.ReadAllBytes(path));
                AssertRequestOrder(HttpVerb.Post, HttpVerb.Get);
                Assert.AreEqual("https://example.test/api/user/me", _transport.Requests.Last().Url);
                Assert.That(_transport.Requests.All(r => r.Headers["Authorization"] == "Bearer access-a"));
            }
            finally
            {
                if (File.Exists(path)) { File.Delete(path); }
            }
        }

        [Test(Description = "end 성공 뒤 user/me 조회만 실패하면 재시도는 GET만 하고 end를 다시 보내지 않는다")]
        public void SnapshotFailure_AfterAcknowledgement_RetryOnlyReads()
        {
            _transport.Queued.Enqueue(new HttpResponseData(200, ""));
            _transport.Queued.Enqueue(new HttpResponseData(500, ""));
            Assert.Throws<ApiException>(() => _reconciler.Submit().GetAwaiter().GetResult());
            Assert.AreEqual(BattleSubmissionStatus.Acknowledged, _reconciler.SubmissionStatus);

            var result = _reconciler.Submit().GetAwaiter().GetResult();

            Assert.AreEqual(BattleSubmissionStatus.Acknowledged, result.SubmissionStatus);
            AssertRequestOrder(HttpVerb.Post, HttpVerb.Get, HttpVerb.Get);
        }

        [TestCase(404)]
        [TestCase(500)]
        [Test(Description = "404/5xx 뒤 최신 재화·진척이 보여도 이 전투의 저장 성공이라고 추정하지 않는다")]
        public void UncertainSubmission_ObservedProgress_DoesNotConfirmBattle(int status)
        {
            _transport.Queued.Enqueue(new HttpResponseData(status, ""));
            var result = _reconciler.Submit().GetAwaiter().GetResult();
            Assert.AreEqual(BattleSubmissionStatus.Unknown, result.SubmissionStatus);
            Assert.AreEqual($"HTTP_{status}", result.SubmissionErrorCode);
            Assert.AreEqual(3, result.ObservedState.stageProgress.Single().clearRating);

            result = _reconciler.RefreshState().GetAwaiter().GetResult();
            Assert.AreEqual(BattleSubmissionStatus.Unknown, result.SubmissionStatus);
            AssertRequestOrder(HttpVerb.Post, HttpVerb.Get, HttpVerb.Get);
        }

        [Test(Description = "POST 응답 유실 뒤 조회와 재호출이 성공해도 end를 자동/수동 중복 전송하지 않는다")]
        public void LostResponse_RepeatedSubmit_OnlyReadsAndRemainsUnknown()
        {
            _transport.Queued.Enqueue(HttpResponseData.Failed(HttpFailure.Timeout));
            var first = _reconciler.Submit().GetAwaiter().GetResult();
            var second = _reconciler.Submit().GetAwaiter().GetResult();
            Assert.AreEqual(HttpErrorMapper.Timeout, first.SubmissionErrorCode);
            Assert.AreEqual(BattleSubmissionStatus.Unknown, second.SubmissionStatus);
            AssertRequestOrder(HttpVerb.Post, HttpVerb.Get, HttpVerb.Get);
        }

        [Test(Description = "서버 거절과 관측 상태를 함께 돌려주며 거절 뒤 조회 성공을 제출 확인으로 바꾸지 않는다")]
        public void RejectedSubmission_RemainsRejectedAfterReading()
        {
            _transport.Queued.Enqueue(new HttpResponseData(400, ""));
            var result = _reconciler.Submit().GetAwaiter().GetResult();
            Assert.AreEqual(BattleSubmissionStatus.Rejected, result.SubmissionStatus);
            Assert.AreEqual("HTTP_400", result.SubmissionErrorCode);
            Assert.AreEqual(80, result.ObservedState.gold);
        }

        [TestCase("{}")]
        [TestCase("{\"gold\":80}")]
        [Test(Description = "부분 응답을 0/빈 목록 상태로 바꾸지 않으며 후속 재조회에도 POST는 늘리지 않는다")]
        public void PartialSnapshot_IsRejected_AndCanBeReadAgain(string body)
        {
            _transport.Queued.Enqueue(new HttpResponseData(200, ""));
            _transport.Queued.Enqueue(new HttpResponseData(200, body));
            var error = Assert.Throws<ApiException>(() => _reconciler.Submit().GetAwaiter().GetResult());
            Assert.AreEqual(HttpErrorMapper.InvalidResponse, error.Code);
            _reconciler.RefreshState().GetAwaiter().GetResult();
            AssertRequestOrder(HttpVerb.Post, HttpVerb.Get, HttpVerb.Get);
        }

        [TestCase("items", "[{\"itemId\":\"book.arrow\",\"quantity\":-1}]")]
        [TestCase("items", "[{\"itemId\":\"book.arrow\",\"quantity\":1},{\"itemId\":\"book.arrow\",\"quantity\":2}]")]
        [TestCase("stageProgress", "[{\"stageId\":7,\"clearRating\":1,\"claimedRating\":3}]")]
        [Test(Description = "음수·중복 아이템이나 클리어보다 높은 수령 등급을 관측 상태로 내보내지 않는다")]
        public void InconsistentSnapshot_IsRejected(string field, string rows)
        {
            var state = JObject.Parse(State);
            state[field] = JArray.Parse(rows);
            _transport.Queued.Enqueue(new HttpResponseData(200, ""));
            _transport.Queued.Enqueue(new HttpResponseData(200, state.ToString()));
            var error = Assert.Throws<ApiException>(() => _reconciler.Submit().GetAwaiter().GetResult());
            Assert.AreEqual(HttpErrorMapper.InvalidResponse, error.Code);
            Assert.AreEqual(BattleSubmissionStatus.Acknowledged, _reconciler.SubmissionStatus);
        }

        [Test(Description = "취소된 제출은 전송하지 않으며 아직 제출 전인 조정기의 GET도 거절한다")]
        public void PreCancelledSubmission_DoesNotMarkAttempted()
        {
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            Assert.Throws<OperationCanceledException>(() => _reconciler.Submit(cancelled.Token).GetAwaiter().GetResult());
            Assert.AreEqual(BattleSubmissionStatus.NotSubmitted, _reconciler.SubmissionStatus);
            Assert.Throws<InvalidOperationException>(() => _reconciler.RefreshState().GetAwaiter().GetResult());
            Assert.IsEmpty(_transport.Requests);
        }

        [Test(Description = "전송 중 연타를 막고 전송 후 계정 전환이 있으면 새 계정 조회·이전 응답 노출을 하지 않는다")]
        public void InFlightSubmission_BlocksConcurrentCalls_AndAccountChange()
        {
            var pending = new DeferredTransport();
            var reconciler = Create(pending, _tokens);
            var task = reconciler.Submit();
            Assert.Throws<InvalidOperationException>(() => reconciler.Submit().GetAwaiter().GetResult());
            _tokens.Set("access-b", "refresh-b");
            pending.Complete(new HttpResponseData(200, ""));
            var error = Assert.Throws<ApiException>(() => task.GetAwaiter().GetResult());
            Assert.AreEqual(HttpErrorMapper.SessionExpired, error.Code);
            Assert.AreEqual(1, pending.Requests.Count);
        }

        [Test(Description = "헤더 생성 중 계정이 바뀌어도 새 계정으로 end를 보내지 않는다")]
        public void AccountChangesWhileBuildingHeaders_DoesNotSend()
        {
            var tokens = new SwitchingHeaders(_tokens);
            var reconciler = Create(_transport, tokens);
            var error = Assert.Throws<ApiException>(() => reconciler.Submit().GetAwaiter().GetResult());
            Assert.AreEqual(HttpErrorMapper.SessionExpired, error.Code);
            Assert.IsEmpty(_transport.Requests);
        }

        [Test(Description = "전송 중 취소도 적용 여부 불명으로 남기고 이후 복구는 GET만 한다")]
        public void CancelWhileSending_RetainsUnknown_AndOnlyReadsAfterward()
        {
            var transport = new DeferredTransport();
            var reconciler = Create(transport, _tokens);
            using var cancellation = new CancellationTokenSource();
            var task = reconciler.Submit(cancellation.Token);
            cancellation.Cancel();
            Assert.Throws<OperationCanceledException>(() => task.GetAwaiter().GetResult());
            transport.Complete(new HttpResponseData(200, ""));
            var result = reconciler.RefreshState().GetAwaiter().GetResult();
            Assert.AreEqual(BattleSubmissionStatus.Unknown, result.SubmissionStatus);
            Assert.AreEqual(1, transport.Requests.Count(r => r.Verb == HttpVerb.Post));
            Assert.AreEqual(HttpVerb.Get, transport.Requests.Last().Verb);
        }

        [Test(Description = "user/me 요청 중 계정이 바뀌면 이전 계정의 늦은 상태도 반환하지 않는다")]
        public void AccountChangesDuringSnapshot_DiscardsLateState()
        {
            var transport = new DeferredTransport(deferGet: true);
            var reconciler = Create(transport, _tokens);
            var task = reconciler.Submit();
            _tokens.Set("access-b", "refresh-b");
            transport.Complete(new HttpResponseData(200, State));
            var error = Assert.Throws<ApiException>(() => task.GetAwaiter().GetResult());
            Assert.AreEqual(HttpErrorMapper.SessionExpired, error.Code);
            Assert.Throws<ApiException>(() => reconciler.RefreshState().GetAwaiter().GetResult());
            Assert.AreEqual(2, transport.Requests.Count);
        }

        [Test(Description = "같은 로그인 세대의 토큰 갱신은 허용하고 재조회만 새 Bearer로 이어간다")]
        public void SnapshotTokenRefresh_DoesNotRepeatEnd()
        {
            _transport.Queued.Enqueue(new HttpResponseData(200, ""));
            _transport.Queued.Enqueue(new HttpResponseData(401, ""));
            _transport.Queued.Enqueue(new HttpResponseData(200, "{\"accessToken\":\"new-access\",\"refreshToken\":\"new-refresh\"}"));
            var result = _reconciler.Submit().GetAwaiter().GetResult();
            Assert.AreEqual(BattleSubmissionStatus.Acknowledged, result.SubmissionStatus);
            Assert.AreEqual(1, _transport.Requests.Count(r => r.Url.EndsWith("api/battle/end")));
            Assert.AreEqual("Bearer new-access", _transport.Requests.Last().Headers["Authorization"]);
        }

        private static HttpBattleResultReconciler Create(IHttpTransport transport, IAuthTokenStore tokens) =>
            new(new HttpApiConfig("https://example.test"), transport, tokens, new BattleEndRequest("battle", 7, 20, 3));

        private void AssertRequestOrder(params HttpVerb[] verbs) =>
            CollectionAssert.AreEqual(verbs, _transport.Requests.Select(r => r.Verb).ToArray());

        private sealed class DeferredTransport : IHttpTransport
        {
            private readonly UniTaskCompletionSource<HttpResponseData> _pending = new();
            private readonly bool _deferGet;
            public readonly List<HttpRequestData> Requests = new();
            public DeferredTransport(bool deferGet = false) { _deferGet = deferGet; }
            public void Complete(HttpResponseData response) => _pending.TrySetResult(response);
            public UniTask<HttpResponseData> Send(HttpRequestData request, float timeoutSeconds, CancellationToken cancellationToken)
            {
                Requests.Add(request);
                return request.Verb == (_deferGet ? HttpVerb.Get : HttpVerb.Post)
                    ? _pending.Task.AttachExternalCancellation(cancellationToken)
                    : UniTask.FromResult(new HttpResponseData(200, request.Verb == HttpVerb.Get ? State : ""));
            }
        }

        private sealed class SwitchingHeaders : IAuthTokenStore
        {
            private readonly AuthSession _session;
            public SwitchingHeaders(AuthSession session) { _session = session; }
            public string AccessToken => _session.AccessToken;
            public string RefreshToken => _session.RefreshToken;
            public long Generation => _session.Generation;
            public void Set(string access, string refresh) => _session.Set(access, refresh);
            public void Clear() => _session.Clear();
            public bool TryRefresh(long generation, string access, string refresh) => _session.TryRefresh(generation, access, refresh);
            public bool TryClear(long generation) => _session.TryClear(generation);
            public IReadOnlyDictionary<string, string> GetHeaders()
            {
                _session.Set("access-b", "refresh-b");
                return _session.GetHeaders();
            }
        }
    }
}
