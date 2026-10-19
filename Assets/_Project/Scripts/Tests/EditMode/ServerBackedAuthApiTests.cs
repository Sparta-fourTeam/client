using System;
using System.IO;
using Game.Core;
using Game.Network;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>서버 인증(게스트 가입·로그인, 구글 소셜 로그인)이 서버 계약대로 요청하고, 토큰을 세션에 담고, 로컬 로그인까지 이어가는지 본다</summary>
    public sealed class ServerBackedAuthApiTests
    {
        private const string Tokens = "{\"accessToken\":\"access-1\",\"refreshToken\":\"refresh-1\"}";

        private string _filePath;
        private LocalSaveStore _store;
        private FakeHttpTransport _transport;
        private AuthSession _session;
        private ServerBackedAuthApi _api;

        [SetUp]
        public void SetUp()
        {
            _filePath = Path.Combine(Path.GetTempPath(), $"server-auth-test-{Guid.NewGuid()}.json");
            _store = new LocalSaveStore(_filePath);
            _transport = new FakeHttpTransport();
            _session = new AuthSession();
            var client = new HttpApiClient(new HttpApiConfig("http://server.test"), _transport, _session);
            _api = new ServerBackedAuthApi(client, _session, new LocalAuthApi(_store, new GameDataStore()));
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_filePath)) { File.Delete(_filePath); }
        }

        private LoginResponse Login(LoginMethod method, string accountId) => _api.Login(method, accountId).GetAwaiter().GetResult();

        [TestCase(RuntimePlatform.Android, null, true)]
        [TestCase(RuntimePlatform.Android, "", true)]
        [TestCase(RuntimePlatform.Android, "0", false)]
        [TestCase(RuntimePlatform.Android, "1", true)]
        [TestCase(RuntimePlatform.OSXEditor, "", false)]
        [TestCase(RuntimePlatform.WindowsEditor, "", false)]
        [TestCase(RuntimePlatform.OSXEditor, "1", true)]
        [TestCase(RuntimePlatform.OSXEditor, "0", false)]
        public void ServerAuth_AndroidReleaseDefaultAndExplicitOverrides(RuntimePlatform platform, string preference, bool expected)
        {
            Assert.That(ServerConfig.ResolveServerAuth(platform, preference), Is.EqualTo(expected));
        }

        [Test]
        public void ServerAuthOverride_UsesTheSameAccountForServerProgress()
        {
            const string authKey = "dev.server_auth", progressKey = "dev.server_progress";
            bool hadAuth = PlayerPrefs.HasKey(authKey), hadProgress = PlayerPrefs.HasKey(progressKey);
            string auth = PlayerPrefs.GetString(authKey), progress = PlayerPrefs.GetString(progressKey);
            bool hadEditorAuth = EditorPrefs.HasKey(ServerConfig.EditorAuthPreferenceKey);
            bool editorAuth = EditorPrefs.GetBool(ServerConfig.EditorAuthPreferenceKey);
            try
            {
                PlayerPrefs.SetString(authKey, "0");
                EditorPrefs.SetBool(ServerConfig.EditorAuthPreferenceKey, true);
                Assert.That(ServerConfig.UseServerAuth, Is.True);
                PlayerPrefs.DeleteKey(progressKey);
                Assert.That(ServerConfig.UseServerPlayer, Is.False);
                PlayerPrefs.SetString(progressKey, "1");
                Assert.That(ServerConfig.UseServerPlayer, Is.True);
                PlayerPrefs.SetString(authKey, "1");
                EditorPrefs.SetBool(ServerConfig.EditorAuthPreferenceKey, false);
                Assert.That(ServerConfig.UseServerAuth, Is.False);
                Assert.That(ServerConfig.UseServerPlayer, Is.False);
            }
            finally
            {
                if (hadAuth) { PlayerPrefs.SetString(authKey, auth); } else { PlayerPrefs.DeleteKey(authKey); }
                if (hadProgress) { PlayerPrefs.SetString(progressKey, progress); } else { PlayerPrefs.DeleteKey(progressKey); }
                if (hadEditorAuth) { EditorPrefs.SetBool(ServerConfig.EditorAuthPreferenceKey, editorAuth); }
                else { EditorPrefs.DeleteKey(ServerConfig.EditorAuthPreferenceKey); }
            }
        }

        [Test]
        public void ServerProgressBuildDefine_RequiresServerAuthentication()
        {
            Assert.IsFalse(ServerConfig.ResolveServerProgress(false, true, "1"));
            Assert.IsTrue(ServerConfig.ResolveServerProgress(true, true, string.Empty));
            Assert.IsFalse(ServerConfig.ResolveServerProgress(true, false, string.Empty));
        }

        [Test(Description = "구글은 provider GOOGLE과 ID 토큰을 소셜 로그인 경로로 보내고, 받은 토큰을 세션에 담는다")]
        public void Google_PostsIdTokenAndStoresTokens()
        {
            _transport.Response = new HttpResponseData(200, Tokens);

            var response = Login(LoginMethod.Google, "id-token-1");

            Assert.AreEqual(1, _transport.Requests.Count);
            var request = _transport.Requests[0];
            Assert.AreEqual(HttpVerb.Post, request.Verb);
            Assert.AreEqual("http://server.test/api/auth/social/login", request.Url);
            StringAssert.Contains("\"provider\":\"GOOGLE\"", request.Body);
            StringAssert.Contains("\"idToken\":\"id-token-1\"", request.Body);
            Assert.AreEqual("access-1", response.accessToken);
            Assert.AreEqual("refresh-1", response.refreshToken);
            Assert.AreEqual("access-1", _session.AccessToken);
            Assert.IsNull(response.accountId, "구글은 서버가 계정 ID를 따로 정해 주지 않는다");
        }

        [Test(Description = "저장된 게스트 ID가 없으면 가입하고, 서버가 정해 준 게스트 ID를 응답으로 돌려준다")]
        public void Guest_WithoutId_SignsUp()
        {
            _transport.Response = new HttpResponseData(200, "{\"guestId\":\"guest-1\",\"tokens\":" + Tokens + "}");

            var response = Login(LoginMethod.Guest, null);

            Assert.AreEqual(1, _transport.Requests.Count);
            Assert.AreEqual("http://server.test/api/auth/guest/signup", _transport.Requests[0].Url);
            Assert.AreEqual("guest-1", response.accountId);
            Assert.AreEqual("access-1", _session.AccessToken);
        }

        [Test(Description = "저장된 게스트 ID가 있으면 가입하지 않고 그 ID로 로그인한다")]
        public void Guest_WithId_LogsIn()
        {
            _transport.Response = new HttpResponseData(200, Tokens);

            var response = Login(LoginMethod.Guest, "guest-1");

            Assert.AreEqual(1, _transport.Requests.Count);
            Assert.AreEqual("http://server.test/api/auth/guest/login", _transport.Requests[0].Url);
            StringAssert.Contains("\"guestId\":\"guest-1\"", _transport.Requests[0].Body);
            Assert.AreEqual("guest-1", response.accountId);
        }

        [Test(Description = "서버가 모르는 게스트 ID(401)나 형식이 틀린 ID(400)면 새로 가입하고 새 게스트 ID를 돌려준다")]
        public void Guest_RejectedId_SignsUpAgain([Values(400, 401)] int status)
        {
            _transport.Queued.Enqueue(new HttpResponseData(status, "{}"));
            _transport.Queued.Enqueue(new HttpResponseData(200, "{\"guestId\":\"guest-2\",\"tokens\":" + Tokens + "}"));

            var response = Login(LoginMethod.Guest, "old-id");

            Assert.AreEqual(2, _transport.Requests.Count);
            Assert.AreEqual("http://server.test/api/auth/guest/login", _transport.Requests[0].Url);
            Assert.AreEqual("http://server.test/api/auth/guest/signup", _transport.Requests[1].Url);
            Assert.AreEqual("guest-2", response.accountId);
        }

        [Test(Description = "서버에 닿지 못하면 가입으로 넘어가지 않고 예외를 던지며 세션도 로컬 저장도 만들지 않는다")]
        public void Guest_NetworkFailure_Throws()
        {
            _transport.Response = HttpResponseData.Failed(HttpFailure.Network);

            var error = Assert.Throws<ApiException>(() => Login(LoginMethod.Guest, "guest-1"));

            Assert.AreEqual(ApiErrorKind.Network, error.Kind);
            Assert.AreEqual(1, _transport.Requests.Count, "가입을 이어서 시도하지 않는다");
            Assert.IsFalse(_session.IsSignedIn);
            Assert.IsFalse(_store.Exists(), "서버 로그인이 실패하면 로컬 로그인도 하지 않는다");
        }

        [Test(Description = "ID 토큰 검증에 실패하면(401) 예외를 던지고 토큰을 담지 않는다")]
        public void Google_InvalidToken_Throws()
        {
            _transport.Response = new HttpResponseData(401, "{}");

            var error = Assert.Throws<ApiException>(() => Login(LoginMethod.Google, "bad-token"));

            Assert.AreEqual("HTTP_401", error.Code);
            Assert.IsFalse(_session.IsSignedIn);
        }

        [Test(Description = "토큰이 빠진 응답은 계약 위반으로 거절한다")]
        public void Google_ResponseWithoutTokens_IsRejected()
        {
            _transport.Response = new HttpResponseData(200, "{\"accessToken\":\"only-access\"}");

            var error = Assert.Throws<ApiException>(() => Login(LoginMethod.Google, "id-token-1"));

            Assert.AreEqual(HttpErrorMapper.InvalidResponse, error.Code);
            Assert.IsFalse(_session.IsSignedIn);
        }

        [Test(Description = "서버 로그인에 성공하면 이어서 로컬 로그인도 해서 저장 파일이 만들어진다")]
        public void Success_AlsoCreatesLocalSave()
        {
            _transport.Response = new HttpResponseData(200, Tokens);

            Login(LoginMethod.Google, "id-token-1");

            Assert.IsTrue(_store.Exists());
        }

        [Test(Description = "세션에 토큰이 있으면 Authorization 헤더로 붙이고, 비우면 붙이지 않는다")]
        public void Session_AddsBearerHeaderOnlyWhenSignedIn()
        {
            Assert.IsEmpty(_session.GetHeaders());

            _session.Set("access-1", "refresh-1");
            Assert.AreEqual("Bearer access-1", _session.GetHeaders()["Authorization"]);

            _session.Clear();
            Assert.IsEmpty(_session.GetHeaders());
        }
    }
}
