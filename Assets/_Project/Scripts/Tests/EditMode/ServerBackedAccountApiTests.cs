using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;
using Game.Network;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>서버 계정 연동·삭제와 삭제 후 로컬 정리의 계약.</summary>
    public sealed class ServerBackedAccountApiTests
    {
        private string _filePath;
        private string _directory;
        private LocalSaveStore _store;
        private FakeHttpTransport _transport;
        private ServerBackedAccountApi _api;
        private AuthSession _session;
        private HttpApiClient _client;

        private sealed class Credentials : IAuthCredentialStore
        {
            public LoginMethod Method { get; private set; }
            public string AccountId { get; private set; }
            public bool HasCredential => AccountId != null;
            public void Save(LoginMethod method, string accountId) { Method = method; AccountId = accountId; }
            public void Clear() => AccountId = null;
        }

        private sealed class Navigator : ISceneNavigator
        {
            public int LoadingCalls;
            public SceneId Current => default;
            public UniTask GoToLoading() { LoadingCalls++; return UniTask.CompletedTask; }
            public UniTask GoToLobby() => UniTask.CompletedTask;
            public UniTask GoToStage(int stageId) => UniTask.CompletedTask;
            public UniTask RestartStage() => UniTask.CompletedTask;
        }

        private sealed class PendingTransport : IHttpTransport
        {
            public readonly UniTaskCompletionSource<HttpResponseData> Completion = new();
            public UniTask<HttpResponseData> Send(HttpRequestData request, float timeoutSeconds, CancellationToken cancellationToken) => Completion.Task;
        }

        private sealed class Google : ISocialSignIn
        {
            public LoginMethod Method => LoginMethod.Google;
            public bool IsAvailable => true;
            public UniTask<string> SignInAsync() => UniTask.FromResult("same-google-id-token");
        }

        private sealed class Prompt : ILoginPrompt
        {
            public UniTask<bool> WaitForServiceConsentAsync() => UniTask.FromResult(true);
            public UniTask<bool> WaitForAnalyticsConsentAsync() => UniTask.FromResult(false);
            public int Choices;
            public UniTask<LoginMethod> WaitForSignInAsync(IReadOnlyCollection<LoginMethod> available)
            { Choices++; return UniTask.FromResult(LoginMethod.Google); }
            public UniTask WaitForRetryAsync() => throw new InvalidOperationException("unexpected login failure");
            public void ShowSigningIn() { }
            public void Hide() { }
        }

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), $"account-test-{Guid.NewGuid()}");
            Directory.CreateDirectory(_directory);
            _filePath = Path.Combine(_directory, "save.json");
            _store = new LocalSaveStore(_filePath);
            _transport = new FakeHttpTransport { Response = new HttpResponseData(204, string.Empty) };
            _session = new AuthSession();
            _session.Set("guest-access", "guest-refresh");
            _client = new HttpApiClient(new HttpApiConfig("https://example.test", 7f), _transport, _session, tokens: _session);
            _api = new ServerBackedAccountApi(_client, new LocalAccountApi(_store));
        }

        [TearDown]
        public void TearDown()
        {
            File.SetAttributes(_directory, FileAttributes.Normal);
            _store.Delete();
            Directory.Delete(_directory, true);
        }

        [Test(Description = "구글 ID 토큰을 게스트 토큰과 함께 POST /api/auth/social/link로 보내고, 204면 성공한다")]
        public void Link_SendsProviderAndIdTokenWithBearer()
        {
            var before = _store.Load();
            before.nickname = "연동전이름";
            before.exp = 123;
            _store.Flush(before);
            _api.LinkAccount(LoginMethod.Google, "google-id-token").GetAwaiter().GetResult();
            var after = _store.Load();
            Assert.That(after.nickname, Is.EqualTo("연동전이름"));
            Assert.That(after.playerId, Is.EqualTo(before.playerId));
            Assert.That(after.exp, Is.EqualTo(123));

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

            Assert.AreEqual(status == 401 ? HttpErrorMapper.SessionExpired : $"HTTP_{status}", ex.Code);
            Assert.AreEqual(ApiErrorKind.Rejected, ex.Kind);
            Assert.AreEqual(status == 401 ? 2 : 1, _transport.Requests.Count);
            if (status == 401) { Assert.IsFalse(_session.IsSignedIn); }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Link_ExpiredAccess_RefreshesOnceBeforeRetry(bool refreshSucceeds)
        {
            var session = new AuthSession();
            session.Set("old-access", "old-refresh");
            var client = new HttpApiClient(new HttpApiConfig("https://example.test"), _transport, tokens: session);
            var api = new ServerBackedAccountApi(client, new LocalAccountApi(_store));
            _transport.Queued.Enqueue(new HttpResponseData(401, "{}"));
            _transport.Queued.Enqueue(refreshSucceeds
                ? new HttpResponseData(200, "{\"accessToken\":\"new-access\",\"refreshToken\":\"new-refresh\"}")
                : new HttpResponseData(401, "{}"));
            _transport.Queued.Enqueue(new HttpResponseData(204, null));

            if (refreshSucceeds)
            {
                api.LinkAccount(LoginMethod.Google, "google-id-token").GetAwaiter().GetResult();
                Assert.AreEqual(3, _transport.Requests.Count);
                Assert.AreEqual("Bearer new-access", _transport.Requests[2].Headers["Authorization"]);
                Assert.AreEqual(_transport.Requests[0].Body, _transport.Requests[2].Body);
                Assert.AreEqual(HttpVerb.Post, _transport.Requests[2].Verb);
                Assert.AreEqual("new-refresh", session.RefreshToken);
            }
            else
            {
                var error = Assert.Throws<ApiException>(() => api.LinkAccount(LoginMethod.Google, "google-id-token").GetAwaiter().GetResult());
                Assert.AreEqual(HttpErrorMapper.SessionExpired, error.Code);
                Assert.AreEqual(2, _transport.Requests.Count);
                Assert.IsFalse(session.IsSignedIn);
            }

            Assert.AreEqual("https://example.test/api/auth/refresh", _transport.Requests[1].Url);
            Assert.IsFalse(_transport.Requests[1].Headers.ContainsKey("Authorization"));
        }

        [Test(Description = "게스트는 연동할 외부 계정이 아니므로 서버에 보내지 않는다")]
        public void Link_Guest_IsRejectedBeforeSending()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _api.LinkAccount(LoginMethod.Guest, "g").GetAwaiter().GetResult());
            Assert.AreEqual(0, _transport.Requests.Count);
        }

        private (AccountService service, Credentials credentials, Navigator nav, PlayerProfile profile) DeletionFlow(
            LoginMethod method = LoginMethod.Google, IAccountApi api = null)
        {
            var save = _store.Load();
            save.nickname = "old-player";
            save.exp = 900;
            save.wallet.gold = 321;
            save.stageProgress.Add(new StageProgressRow { stageId = 9, clearRating = 3 });
            save.tutorials = new List<string> { "old-tutorial" };
            _store.Flush(save);
            var profile = new PlayerProfile(new GameDataStore());
            profile.Apply(new LocalPlayerApi(_store).GetMe().GetAwaiter().GetResult());
            var credentials = new Credentials();
            credentials.Save(method, method == LoginMethod.Guest ? "guest-secret" : AuthCredentials.SocialAccountId);
            var nav = new Navigator();
            return (new AccountService(api ?? _api, new SessionAuthCredentialStore(credentials, _session), nav, null, profile),
                credentials, nav, profile);
        }

        [TestCase(LoginMethod.Google)]
        [TestCase(LoginMethod.Guest)]
        public void Delete_Confirmed204_ClearsSaveSessionProfileAndReturnsToLogin(LoginMethod method)
        {
            var (service, credentials, nav, profile) = DeletionFlow(method);
            service.DeleteAccountAsync().GetAwaiter().GetResult();

            var request = _transport.Requests[0];
            Assert.AreEqual(HttpVerb.Delete, request.Verb);
            Assert.AreEqual("https://example.test/api/auth/me", request.Url);
            Assert.AreEqual("Bearer guest-access", request.Headers["Authorization"]);
            Assert.IsNull(request.Body);
            Assert.IsFalse(_store.Exists());
            Assert.IsFalse(credentials.HasCredential);
            Assert.IsFalse(_session.IsSignedIn);
            Assert.IsNull(_session.RefreshToken);
            Assert.IsFalse(profile.IsLoaded);
            Assert.AreEqual(0, profile.Exp);
            Assert.IsFalse(profile.IsStageCleared(9));
            Assert.AreEqual(1, nav.LoadingCalls);
        }

        [Test]
        public void Delete_Confirmed204ButDiskCleanupFails_FinalizesSessionAndRetriesBeforeLoadingOldSave()
        {
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                Assert.Ignore("이 회귀는 Unix 디렉터리 쓰기 권한으로 실제 삭제 실패를 재현한다");
            }

            var (service, credentials, nav, profile) = DeletionFlow();
            var attributes = File.GetAttributes(_directory);
            File.SetAttributes(_directory, attributes | FileAttributes.ReadOnly);
            try
            {
                service.DeleteAccountAsync().GetAwaiter().GetResult();
                Assert.IsTrue(File.Exists(_filePath), "권한 오류로 실제 파일은 아직 남아 있다");
                Assert.IsFalse(credentials.HasCredential);
                Assert.IsFalse(_session.IsSignedIn);
                Assert.IsFalse(profile.IsLoaded);
                Assert.AreEqual(0, profile.Exp);
                Assert.AreEqual(1, nav.LoadingCalls);

                var restarted = new LocalSaveStore(_filePath);
                var error = Assert.Catch(() => restarted.Load());
                Assert.IsTrue(error is IOException || error is UnauthorizedAccessException,
                    "정리가 끝나기 전에는 이전 진행을 읽지 않는다");
                File.SetAttributes(_directory, attributes);
                Assert.IsFalse(restarted.Exists(), "새 인스턴스에서도 미완료 정리를 재시도해 기존 파일을 지운다");
                Assert.AreEqual(0, restarted.Load().exp);
                Assert.AreEqual(0, restarted.Load().wallet.gold);
                Assert.AreEqual(1, _transport.Requests.Count, "서버 삭제를 다시 요청하지 않고 로컬만 복구한다");
            }
            finally { File.SetAttributes(_directory, attributes); }
        }

        [TestCase(401)]
        [TestCase(403)]
        [TestCase(404)]
        [TestCase(500)]
        [TestCase(503)]
        [TestCase(200)]
        [TestCase(202)]
        [TestCase(0, HttpFailure.Network)]
        [TestCase(0, HttpFailure.Timeout)]
        public void Delete_NotConfirmed_PreservesProgressCredentialsAndScreen(int status, HttpFailure failure = HttpFailure.None)
        {
            var (service, credentials, nav, profile) = DeletionFlow();
            _transport.Response = new HttpResponseData(status, "{}", failure);
            string original = File.ReadAllText(_filePath);

            Assert.Throws<ApiException>(() => service.DeleteAccountAsync().GetAwaiter().GetResult());

            Assert.AreEqual(original, File.ReadAllText(_filePath));
            Assert.IsTrue(credentials.HasCredential);
            Assert.IsTrue(profile.IsLoaded);
            Assert.AreEqual(900, profile.Exp);
            Assert.AreEqual(0, nav.LoadingCalls);
            if (status != 401) { Assert.AreEqual("guest-access", _session.AccessToken); }
            Assert.AreEqual(status == 401 ? 2 : 1, _transport.Requests.Count, "서버/통신 오류에 삭제를 자동 재전송하지 않는다");
        }

        [Test]
        public void Delete_WithoutToken_Server500StillPreservesLocalAccount()
        {
            var (service, credentials, nav, profile) = DeletionFlow();
            _session.Clear();
            _transport.Response = new HttpResponseData(500, "{}");
            Assert.Throws<ApiException>(() => service.DeleteAccountAsync().GetAwaiter().GetResult());
            Assert.IsTrue(_store.Exists());
            Assert.IsTrue(credentials.HasCredential);
            Assert.AreEqual(900, profile.Exp);
            Assert.AreEqual(0, nav.LoadingCalls);
            Assert.AreEqual(1, _transport.Requests.Count);
            Assert.IsFalse(_transport.Requests[0].Headers.ContainsKey("Authorization"));
        }

        [Test]
        public void Delete_WaitsForServerBeforeClearingAnything()
        {
            var transport = new PendingTransport();
            var client = new HttpApiClient(new HttpApiConfig("https://example.test"), transport, _session);
            var (service, credentials, nav, profile) = DeletionFlow(api: new ServerBackedAccountApi(client, new LocalAccountApi(_store)));
            var pending = service.DeleteAccountAsync();
            Assert.AreEqual(UniTaskStatus.Pending, pending.Status);
            Assert.IsTrue(_store.Exists());
            Assert.IsTrue(credentials.HasCredential);
            Assert.IsTrue(_session.IsSignedIn);
            Assert.AreEqual(900, profile.Exp);
            Assert.AreEqual(0, nav.LoadingCalls);
            transport.Completion.TrySetResult(new HttpResponseData(204, null));
            pending.GetAwaiter().GetResult();
            Assert.IsFalse(_store.Exists());
            Assert.AreEqual(1, nav.LoadingCalls);
        }

        [Test]
        public void Delete_ExpiredAccess_RefreshesOnceAndDeletesWithNewToken()
        {
            var (service, _, nav, _) = DeletionFlow();
            _transport.Queued.Enqueue(new HttpResponseData(401, "{}"));
            _transport.Queued.Enqueue(new HttpResponseData(200, "{\"accessToken\":\"fresh-access\",\"refreshToken\":\"fresh-refresh\"}"));
            _transport.Queued.Enqueue(new HttpResponseData(204, null));
            service.DeleteAccountAsync().GetAwaiter().GetResult();
            Assert.AreEqual(3, _transport.Requests.Count);
            StringAssert.EndsWith("/api/auth/refresh", _transport.Requests[1].Url);
            Assert.AreEqual("Bearer fresh-access", _transport.Requests[2].Headers["Authorization"]);
            Assert.AreEqual(HttpVerb.Delete, _transport.Requests[2].Verb);
            Assert.AreEqual(1, nav.LoadingCalls);
            Assert.IsFalse(_session.IsSignedIn);
        }

        [Test]
        public void Delete_ThenSameGoogleLogin_StartsWithFreshLocalProgressAndPrompts()
        {
            var (service, credentials, _, profile) = DeletionFlow();
            service.DeleteAccountAsync().GetAwaiter().GetResult();
            var data = new GameDataStore();
            var auth = new ServerBackedAuthApi(_client, _session, new LocalAuthApi(_store, data));
            _transport.Response = new HttpResponseData(200, "{\"accessToken\":\"new-user-access\",\"refreshToken\":\"new-user-refresh\"}");
            var prompt = new Prompt();
            var login = new LoginFlow(auth, new LocalPlayerApi(_store), profile,
                new SessionAuthCredentialStore(credentials, _session), prompt, new[] { new Google() });
            Assert.IsTrue(login.RunAsync().GetAwaiter().GetResult());
            Assert.AreEqual(1, prompt.Choices, "삭제 뒤에는 저장된 인증으로 자동 로그인하지 않는다");
            Assert.AreEqual("new-user-access", _session.AccessToken);
            Assert.AreEqual(0, profile.Exp);
            Assert.AreEqual(0, profile.Gold);
            Assert.IsFalse(profile.IsStageUnlocked(9));
            Assert.IsFalse(profile.IsTutorialDone("old-tutorial"));
            Assert.IsTrue(profile.IsStageUnlocked(1));
            StringAssert.Contains("same-google-id-token", _transport.Requests[1].Body);
            Assert.IsFalse(_transport.Requests[1].Headers.ContainsKey("Authorization"));
            Assert.AreEqual(AuthCredentials.SocialAccountId, credentials.AccountId);
        }
    }
}
