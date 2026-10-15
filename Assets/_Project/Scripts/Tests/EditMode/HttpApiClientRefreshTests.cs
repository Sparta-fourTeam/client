using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;
using Game.Network;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>액세스 토큰이 만료돼 401을 받으면 리프레시 토큰으로 새 토큰을 받아 같은 요청을 한 번만 다시 보낸다.
    /// 리프레시 토큰까지 거절되면 로그인이 필요하다(SESSION_EXPIRED)</summary>
    public sealed class HttpApiClientRefreshTests
    {
        private const string PingOk = "{\"name\":\"ok\",\"count\":1}";
        private const string NewTokens = "{\"accessToken\":\"new-access\",\"refreshToken\":\"new-refresh\"}";

        [Serializable]
        private class Ping
        {
            public string name;
            public int count;
        }

        /// <summary>요청마다 handler가 응답을 정한다</summary>
        private sealed class ScriptedTransport : IHttpTransport
        {
            public Func<HttpRequestData, UniTask<HttpResponseData>> Handler;
            public readonly List<HttpRequestData> Requests = new();

            public UniTask<HttpResponseData> Send(HttpRequestData request, float timeoutSeconds, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Requests.Add(request);
                return Handler(request).AttachExternalCancellation(cancellationToken);
            }

            public int Count(string pathPart) => Requests.FindAll(r => r.Url.Contains(pathPart)).Count;
        }

        private AuthSession _session;
        private ScriptedTransport _transport;
        private HttpApiClient _client;

        [SetUp]
        public void SetUp()
        {
            _session = new AuthSession();
            _session.Set("old-access", "old-refresh");
            _transport = new ScriptedTransport();
            _client = new HttpApiClient(new HttpApiConfig("https://example.test", 7f), _transport, tokens: _session);
        }

        private static UniTask<HttpResponseData> Reply(int status, string body = "") =>
            UniTask.FromResult(new HttpResponseData(status, body));

        private static string Bearer(HttpRequestData request) =>
            request.Headers.TryGetValue("Authorization", out var value) ? value : null;

        private static T Run<T>(UniTask<T> task) => task.GetAwaiter().GetResult();

        // 옛 토큰으로 온 요청은 401, 새 토큰으로 온 요청은 성공, 재발급은 refreshResult
        private void ExpireOldToken(Func<HttpRequestData, UniTask<HttpResponseData>> refresh)
        {
            _transport.Handler = request =>
            {
                if (request.Url.EndsWith("api/auth/refresh")) { return refresh(request); }
                return Bearer(request) == "Bearer new-access" ? Reply(200, PingOk) : Reply(401);
            };
        }

        [Test(Description = "액세스 토큰이 만료돼 401이면 리프레시 토큰으로 새 토큰을 받고, 같은 요청을 새 토큰으로 한 번 다시 보내 성공한다")]
        public void Expired_RefreshesAndRetriesOnce()
        {
            ExpireOldToken(_ => Reply(200, NewTokens));

            var result = Run(_client.Get<Ping>("api/ping"));

            Assert.AreEqual("ok", result.name);
            Assert.AreEqual(3, _transport.Requests.Count);
            Assert.AreEqual("Bearer old-access", Bearer(_transport.Requests[0]));
            StringAssert.Contains("old-refresh", _transport.Requests[1].Body);
            Assert.IsNull(Bearer(_transport.Requests[1]), "재발급 요청에는 만료된 액세스 토큰을 붙이지 않는다");
            Assert.AreEqual("Bearer new-access", Bearer(_transport.Requests[2]));
            Assert.AreEqual("new-access", _session.AccessToken);
            Assert.AreEqual("new-refresh", _session.RefreshToken, "서버는 쓴 리프레시 토큰을 무효로 하므로 새 토큰으로 바꿔 둔다");
        }

        [Test(Description = "리프레시 토큰까지 거절되면 SESSION_EXPIRED를 던지고 토큰을 비우며, 요청을 더 보내지 않는다")]
        public void RefreshRejected_SessionExpiredAndTokensCleared()
        {
            ExpireOldToken(_ => Reply(401));

            var ex = Assert.Throws<ApiException>(() => Run(_client.Get<Ping>("api/ping")));

            Assert.AreEqual(HttpErrorMapper.SessionExpired, ex.Code);
            Assert.AreEqual(2, _transport.Requests.Count, "원래 요청 한 번과 재발급 한 번뿐이다");
            Assert.IsFalse(_session.IsSignedIn);
            Assert.IsNull(_session.RefreshToken);
        }

        [TestCase(0, HttpFailure.Network)]
        [TestCase(0, HttpFailure.Timeout)]
        [TestCase(500, HttpFailure.None)]
        public void FailedRefresh_DoesNotReusePossiblyConsumedToken(int status, HttpFailure failure)
        {
            // 서버가 토큰을 회전시킨 뒤 응답이 유실되거나 오류로 돌아온 경우를 재현한다.
            ExpireOldToken(_ => UniTask.FromResult(new HttpResponseData(status, null, failure)));

            var ex = Assert.Throws<ApiException>(() => Run(_client.Get<Ping>("api/ping")));

            Assert.AreEqual(HttpErrorMapper.SessionExpired, ex.Code);
            Assert.IsFalse(_session.IsSignedIn);
            Assert.IsNull(_session.RefreshToken);
            Assert.Throws<ApiException>(() => Run(_client.Get<Ping>("api/after-failure")));
            Assert.AreEqual(1, _transport.Count("api/auth/refresh"), "소비 여부를 모르는 refresh 토큰을 다시 보내지 않는다");
        }

        [Test(Description = "새 토큰으로 다시 보냈는데도 401이면 더 반복하지 않고 그대로 던진다")]
        public void RetryStillUnauthorized_DoesNotLoop()
        {
            _transport.Handler = request =>
                request.Url.EndsWith("api/auth/refresh") ? Reply(200, NewTokens) : Reply(401);

            var ex = Assert.Throws<ApiException>(() => Run(_client.Get<Ping>("api/ping")));

            Assert.AreEqual("HTTP_401", ex.Code);
            Assert.AreEqual(3, _transport.Requests.Count);
            Assert.AreEqual(1, _transport.Count("api/auth/refresh"));
        }

        [Test(Description = "로그인·재발급 경로의 401은 자격 증명이 틀린 것이라 재발급하지 않는다")]
        public void AuthPaths_NeverRefresh()
        {
            _transport.Handler = _ => Reply(401);

            var ex = Assert.Throws<ApiException>(() => Run(_client.Post<Ping, Ping>("api/auth/guest/login", new Ping())));

            Assert.AreEqual("HTTP_401", ex.Code);
            Assert.AreEqual(1, _transport.Requests.Count);
            Assert.AreEqual("old-access", _session.AccessToken);
        }

        [Test(Description = "로그인하지 않은 채 보낸 요청의 401은 재발급하지 않는다")]
        public void NotSignedIn_NoRefresh()
        {
            _session.Clear();
            _transport.Handler = _ => Reply(401);

            var ex = Assert.Throws<ApiException>(() => Run(_client.Get<Ping>("api/ping")));

            Assert.AreEqual("HTTP_401", ex.Code);
            Assert.AreEqual(1, _transport.Requests.Count);
        }

        [Test(Description = "토큰 저장소 없이 만든 클라이언트는 401을 그대로 던진다")]
        public void WithoutTokenStore_NoRefresh()
        {
            var client = new HttpApiClient(new HttpApiConfig("https://example.test", 7f), _transport);
            _transport.Handler = _ => Reply(401);

            var ex = Assert.Throws<ApiException>(() => Run(client.Get<Ping>("api/ping")));

            Assert.AreEqual("HTTP_401", ex.Code);
            Assert.AreEqual(1, _transport.Requests.Count);
        }

        [Test(Description = "요청 두 개가 동시에 401을 받아도 재발급은 한 번만 보내고, 둘 다 새 토큰으로 다시 보내 성공한다")]
        public void ConcurrentExpiry_RefreshesOnce()
        {
            var gate = new UniTaskCompletionSource<HttpResponseData>();
            ExpireOldToken(_ => gate.Task);

            var first = _client.Get<Ping>("api/ping");
            var second = _client.Get<Ping>("api/other");
            Assert.AreEqual(1, _transport.Count("api/auth/refresh"), "둘 다 401을 받았지만 재발급은 하나만 나간다");

            gate.TrySetResult(new HttpResponseData(200, NewTokens));

            Assert.AreEqual("ok", Run(first).name);
            Assert.AreEqual("ok", Run(second).name);
            Assert.AreEqual(1, _transport.Count("api/auth/refresh"));
            Assert.AreEqual(5, _transport.Requests.Count, "원래 요청 둘, 재발급 하나, 다시 보낸 요청 둘");
        }

        [Test(Description = "재발급을 함께 기다리는 요청을 취소하면 즉시 끝나고, 다른 요청은 같은 재발급으로 성공한다")]
        public void CanceledWaiter_DoesNotWaitForRefreshOrCancelOtherRequest()
        {
            using var cancellation = new CancellationTokenSource();
            var gate = new UniTaskCompletionSource<HttpResponseData>();
            ExpireOldToken(_ => gate.Task);
            var first = _client.Get<Ping>("api/ping");
            var waiter = _client.Get<Ping>("api/other", cancellation.Token);
            try
            {
                cancellation.Cancel();
                Assert.AreEqual(UniTaskStatus.Canceled, waiter.Status, "취소된 요청은 재발급 응답을 기다리지 않는다");
            }
            finally
            {
                gate.TrySetResult(new HttpResponseData(200, NewTokens));
            }

            Assert.Throws<OperationCanceledException>(() => Run(waiter));
            Assert.AreEqual("ok", Run(first).name);
            Assert.AreEqual(1, _transport.Count("api/auth/refresh"));
        }

        [Test(Description = "재발급을 시작한 요청을 취소해도 공유 재발급은 계속되며 회전된 토큰을 중복 사용하지 않는다")]
        public void CanceledInitiator_DoesNotRestartSharedRefresh()
        {
            using var cancellation = new CancellationTokenSource();
            var gate = new UniTaskCompletionSource<HttpResponseData>();
            ExpireOldToken(_ => gate.Task);
            var first = _client.Get<Ping>("api/ping", cancellation.Token);
            var second = _client.Get<Ping>("api/other");
            try
            {
                cancellation.Cancel();
                Assert.AreEqual(UniTaskStatus.Canceled, first.Status);
                Assert.AreEqual(1, _transport.Count("api/auth/refresh"), "서버가 이미 소비했을 수 있는 토큰으로 재발급을 다시 보내지 않는다");
            }
            finally
            {
                gate.TrySetResult(new HttpResponseData(200, NewTokens));
            }

            Assert.Throws<OperationCanceledException>(() => Run(first));
            Assert.AreEqual("ok", Run(second).name);
            Assert.AreEqual("new-refresh", _session.RefreshToken);
            Assert.AreEqual(1, _transport.Count("api/auth/refresh"));
        }

        [TestCase(false, 200, NewTokens, HttpFailure.None)]
        [TestCase(true, 200, NewTokens, HttpFailure.None)]
        [TestCase(true, 401, "", HttpFailure.None)]
        [TestCase(true, 200, "{}", HttpFailure.None)]
        [TestCase(true, 0, "", HttpFailure.Network)]
        public void PendingRefresh_AfterLogoutCannotRestoreOrOverwriteSession(bool signInAgain, int status, string body, HttpFailure failure)
        {
            var gate = new UniTaskCompletionSource<HttpResponseData>();
            ExpireOldToken(_ => gate.Task);
            var pending = _client.Get<Ping>("api/ping");
            try
            {
                Assert.AreEqual(1, _transport.Count("api/auth/refresh"));
                _session.Clear();
                // 같은 문자열로 다시 로그인해도 옛 응답은 새 세션에 속하지 않는다.
                if (signInAgain) { _session.Set("old-access", "old-refresh"); }
            }
            finally
            {
                gate.TrySetResult(new HttpResponseData(status, body, failure));
            }

            var ex = Assert.Throws<ApiException>(() => Run(pending));
            Assert.AreEqual(HttpErrorMapper.SessionExpired, ex.Code);
            Assert.AreEqual(signInAgain ? "old-access" : null, _session.AccessToken);
            Assert.AreEqual(signInAgain ? "old-refresh" : null, _session.RefreshToken);
            Assert.AreEqual(2, _transport.Requests.Count, "옛 요청을 새 로그인으로 재시도하지 않는다");
        }

        [Test]
        public void OldRefreshCompletion_DoesNotDiscardNewSessionsSharedRefresh()
        {
            var oldGate = new UniTaskCompletionSource<HttpResponseData>();
            var newGate = new UniTaskCompletionSource<HttpResponseData>();
            ExpireOldToken(request => request.Body.Contains("other-refresh") ? newGate.Task : oldGate.Task);
            var oldRequest = _client.Get<Ping>("api/old");
            _session.Clear();
            _session.Set("other-access", "other-refresh");
            var newRequest = _client.Get<Ping>("api/new");
            try
            {
                oldGate.TrySetResult(new HttpResponseData(200, NewTokens));
                var ex = Assert.Throws<ApiException>(() => Run(oldRequest));
                Assert.AreEqual(HttpErrorMapper.SessionExpired, ex.Code);
                var sharedRequest = _client.Get<Ping>("api/shared");
                Assert.AreEqual(2, _transport.Count("api/auth/refresh"), "새 세션은 자체 재발급 하나를 계속 공유한다");
                newGate.TrySetResult(new HttpResponseData(200, NewTokens));
                Assert.AreEqual("ok", Run(newRequest).name);
                Assert.AreEqual("ok", Run(sharedRequest).name);
                Assert.AreEqual("new-refresh", _session.RefreshToken);
            }
            finally
            {
                oldGate.TrySetResult(new HttpResponseData(200, NewTokens));
                newGate.TrySetResult(new HttpResponseData(200, NewTokens));
            }
        }

        [Test(Description = "요청을 보낸 뒤 다른 요청이 이미 새 토큰을 받아 두었으면 재발급 없이 새 토큰으로 다시 보낸다")]
        public void AlreadyRefreshedByAnotherRequest_RetriesWithoutRefresh()
        {
            _transport.Handler = request =>
            {
                if (Bearer(request) == "Bearer old-access")
                {
                    Assert.IsTrue(_session.TryRefresh(_session.Generation, "new-access", "new-refresh")); // 같은 세션의 다른 요청이 재발급을 끝냈다
                    return Reply(401);
                }

                return Reply(200, PingOk);
            };

            var result = Run(_client.Get<Ping>("api/ping"));

            Assert.AreEqual("ok", result.name);
            Assert.AreEqual(0, _transport.Count("api/auth/refresh"));
            Assert.AreEqual(2, _transport.Requests.Count);
        }
    }
}
