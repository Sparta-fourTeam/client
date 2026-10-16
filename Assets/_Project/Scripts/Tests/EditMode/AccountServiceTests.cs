using System.Collections.Generic;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using Game.Core;
using Game.Network;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests
{
    /// <summary>로그아웃·계정 삭제·연동 규칙: 게스트는 로그아웃해도 데이터가 사라지고, 소셜 계정은 사라지지 않는다.
    /// 둘 다 저장된 로그인 정보를 지우고 로그인창이 있는 Loading 씬으로 돌아간다</summary>
    public sealed class AccountServiceTests
    {
        private sealed class FakeAccountApi : IAccountApi
        {
            public int Deleted;
            public LoginMethod? Linked;
            public System.Exception LinkError, RenameError;
            public UniTask<string> ChangeNickname(string nickname) => RenameError != null
                ? UniTask.FromException<string>(RenameError) : UniTask.FromResult(nickname);
            public UniTask DeleteAccount() { Deleted++; return UniTask.CompletedTask; }

            public UniTask LinkAccount(LoginMethod method, string accountId)
            {
                if (LinkError != null) { throw LinkError; }
                Linked = method;
                return UniTask.CompletedTask;
            }
        }

        private sealed class MemoryCredentials : IAuthCredentialStore
        {
            public LoginMethod Method { get; private set; }
            public string AccountId { get; private set; }
            public bool HasCredential => AccountId != null;
            public void Save(LoginMethod method, string accountId) { Method = method; AccountId = accountId; }
            public void Clear() => AccountId = null;
        }

        private sealed class FakeNav : ISceneNavigator
        {
            public int LoadingCalls;
            public SceneId Current => default;
            public UniTask GoToLoading() { LoadingCalls++; return UniTask.CompletedTask; }
            public UniTask GoToLobby() => UniTask.CompletedTask;
            public UniTask GoToStage(int stageId) => UniTask.CompletedTask;
            public UniTask RestartStage() => UniTask.CompletedTask;
        }

        private sealed class FakeSocial : ISocialSignIn
        {
            public FakeSocial(bool available, string accountId) { IsAvailable = available; AccountId = accountId; }
            public LoginMethod Method => LoginMethod.Google;
            public bool IsAvailable { get; }
            public string AccountId { get; }
            public int Calls;
            public UniTask<string> SignInAsync() { Calls++; return UniTask.FromResult(AccountId); }
        }

        private FakeAccountApi _api;
        private MemoryCredentials _credentials;
        private FakeNav _nav;
        private AuthSession _session;

        [SetUp]
        public void SetUp()
        {
            _api = new FakeAccountApi();
            _credentials = new MemoryCredentials();
            _nav = new FakeNav();
            _session = new AuthSession();
            _session.Set("access", "refresh");
        }

        private AccountService Create(params ISocialSignIn[] social) => new(_api, new SessionAuthCredentialStore(_credentials, _session), _nav, new List<ISocialSignIn>(social));

        [Test(Description = "닉네임 저장 실패 시 화면 캐시의 이름·ID·진행은 바뀌지 않는다")]
        public void Rename_SaveFails_KeepsCurrentProfile()
        {
            var profile = new PlayerProfile(null);
            profile.Apply(new PlayerSnapshot { nickname = "기존이름", playerId = "public-id", exp = 123 });
            _api.RenameError = new System.IO.IOException("save failed");
            var account = new AccountService(_api, _credentials, _nav, null, profile);
            Assert.Throws<System.IO.IOException>(() => account.ChangeNicknameAsync("새이름").GetAwaiter().GetResult());
            Assert.That(profile.Nickname, Is.EqualTo("기존이름"));
            Assert.That(profile.PlayerId, Is.EqualTo("public-id"));
            Assert.That(profile.Exp, Is.EqualTo(123));
        }

        [Test(Description = "게스트가 로그아웃하면 데이터를 지우고 로그인 정보를 비우고 Loading으로 돌아간다")]
        public void Logout_Guest_DeletesData()
        {
            _credentials.Save(LoginMethod.Guest, "g");

            Create().LogoutAsync().GetAwaiter().GetResult();

            Assert.AreEqual(1, _api.Deleted);
            Assert.IsFalse(_credentials.HasCredential);
            Assert.AreEqual(1, _nav.LoadingCalls);
            Assert.IsNull(_session.AccessToken);
            Assert.IsNull(_session.RefreshToken);
            Assert.IsFalse(_session.GetHeaders().ContainsKey("Authorization"), "로그아웃/삭제 후 서버 인증 헤더가 남으면 안 된다");
        }

        [Test]
        public void Reauthenticate_KeepsGuestIdentityAndProgressInsteadOfDeletingOrSigningUp()
        {
            _credentials.Save(LoginMethod.Guest, "existing-guest");
            _session.Clear();
            var profile = new PlayerProfile(null);
            profile.Apply(new PlayerSnapshot { gold = 321, exp = 900 });
            var service = new AccountService(_api, new SessionAuthCredentialStore(_credentials, _session), _nav, null, profile);
            service.ReauthenticateAsync().GetAwaiter().GetResult();
            Assert.AreEqual(LoginMethod.Guest, _credentials.Method);
            Assert.AreEqual("existing-guest", _credentials.AccountId);
            Assert.IsTrue(profile.IsLoaded);
            Assert.AreEqual(321, profile.Gold);
            Assert.AreEqual(900, profile.Exp);
            Assert.AreEqual(0, _api.Deleted);
            Assert.AreEqual(1, _nav.LoadingCalls);
        }

        [Test(Description = "연동한 계정이 로그아웃하면 데이터는 남기고 로그인 정보만 비운다")]
        public void Logout_Linked_KeepsData()
        {
            _credentials.Save(LoginMethod.Google, "token");

            Create().LogoutAsync().GetAwaiter().GetResult();

            Assert.AreEqual(0, _api.Deleted);
            Assert.IsFalse(_credentials.HasCredential);
            Assert.AreEqual(1, _nav.LoadingCalls);
            Assert.IsNull(_session.AccessToken);
            Assert.IsNull(_session.RefreshToken);
            Assert.IsFalse(_session.GetHeaders().ContainsKey("Authorization"), "로그아웃/삭제 후 서버 인증 헤더가 남으면 안 된다");
        }

        [Test(Description = "계정 삭제는 로그인 방식과 상관없이 데이터를 지우고 Loading으로 돌아간다")]
        public void DeleteAccount_Linked_DeletesData()
        {
            _credentials.Save(LoginMethod.Google, "token");

            Create().DeleteAccountAsync().GetAwaiter().GetResult();

            Assert.AreEqual(1, _api.Deleted);
            Assert.IsFalse(_credentials.HasCredential);
            Assert.AreEqual(1, _nav.LoadingCalls);
            Assert.IsNull(_session.AccessToken);
            Assert.IsNull(_session.RefreshToken);
            Assert.IsFalse(_session.GetHeaders().ContainsKey("Authorization"), "로그아웃/삭제 후 서버 인증 헤더가 남으면 안 된다");
        }

        [Test(Description = "게스트는 쓸 수 있는 외부 계정에 연동되고 로그인 정보가 그 방식으로 바뀐다")]
        public void Link_Guest_SavesLinkedMethod()
        {
            _credentials.Save(LoginMethod.Guest, "g");

            var result = Create(new FakeSocial(true, "google-token")).LinkAsync().GetAwaiter().GetResult();

            Assert.AreEqual(LinkResult.Linked, result);
            Assert.AreEqual(LoginMethod.Google, _api.Linked);
            Assert.AreEqual(LoginMethod.Google, _credentials.Method);
            Assert.AreEqual(AuthCredentials.SocialAccountId, _credentials.AccountId, "ID 토큰은 기기에 저장하지 않는다");
        }

        [Test(Description = "이미 연동된 계정은 외부 로그인을 다시 띄우지 않고, 쓸 수 있는 외부 계정이 없으면 준비 중으로 끝난다")]
        public void Link_AlreadyLinkedOrUnavailable()
        {
            _credentials.Save(LoginMethod.Google, "token");
            var social = new FakeSocial(true, "x");
            Assert.AreEqual(LinkResult.AlreadyLinked, Create(social).LinkAsync().GetAwaiter().GetResult());
            Assert.AreEqual(0, social.Calls);

            _credentials.Save(LoginMethod.Guest, "g");
            Assert.AreEqual(LinkResult.Unavailable, Create(new FakeSocial(false, null)).LinkAsync().GetAwaiter().GetResult());
            Assert.IsNull(_api.Linked);
        }

        [Test(Description = "서버가 연동을 거절하면(409) 로그인 정보를 바꾸지 않고 게스트 그대로 둔다")]
        public void Link_ServerConflict_KeepsGuest()
        {
            _credentials.Save(LoginMethod.Guest, "g");
            _api.LinkError = new ApiException(ApiErrorKind.Rejected, "HTTP_409");
            LogAssert.Expect(LogType.Exception, new Regex("HTTP_409"));

            var result = Create(new FakeSocial(true, "google-token")).LinkAsync().GetAwaiter().GetResult();

            Assert.AreEqual(LinkResult.Conflict, result);
            Assert.AreEqual(LoginMethod.Guest, _credentials.Method);
            Assert.AreEqual("g", _credentials.AccountId);
        }

        [TestCase(ApiErrorKind.Transient, "HTTP_503")]
        [TestCase(ApiErrorKind.Network, "NETWORK_ERROR")]
        [Test(Description = "서버 오류·로그인 만료·네트워크 문제로 연동하지 못하면 실패로 끝나고 게스트 그대로 두며, 다시 시도할 수 있다")]
        public void Link_OtherServerFailure_KeepsGuestAndCanRetry(ApiErrorKind kind, string code)
        {
            _credentials.Save(LoginMethod.Guest, "g");
            _api.LinkError = new ApiException(kind, code);
            LogAssert.Expect(LogType.Exception, new Regex(code));
            var service = Create(new FakeSocial(true, "google-token"));

            Assert.AreEqual(LinkResult.Failed, service.LinkAsync().GetAwaiter().GetResult());
            Assert.AreEqual(LoginMethod.Guest, _credentials.Method);

            _api.LinkError = null;
            Assert.AreEqual(LinkResult.Linked, service.LinkAsync().GetAwaiter().GetResult());
            Assert.AreEqual(LoginMethod.Google, _credentials.Method);
        }

        [TestCase("SESSION_EXPIRED")]
        [TestCase("HTTP_401")]
        public void Link_ExpiredSession_RequiresLoginWithoutReplacingGuest(string code)
        {
            _credentials.Save(LoginMethod.Guest, "original-guest");
            _session.Clear();
            _api.LinkError = new ApiException(ApiErrorKind.Rejected, code);
            LogAssert.Expect(LogType.Exception, new Regex(code));
            var result = Create(new FakeSocial(true, "google-token")).LinkAsync().GetAwaiter().GetResult();

            Assert.AreEqual(LinkResult.ReauthenticationRequired, result);
            Assert.AreEqual(LoginMethod.Guest, _credentials.Method);
            Assert.AreEqual("original-guest", _credentials.AccountId);
            Assert.AreEqual(0, _api.Deleted);
        }

        [TestCase(204, LinkResult.Linked, LoginMethod.Google)]
        [TestCase(409, LinkResult.Conflict, LoginMethod.Guest)]
        public void Link_LostResponse_OnlyConfirmedRetryChangesCredentials(int retryStatus, LinkResult expected, LoginMethod expectedMethod)
        {
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"link-retry-{System.Guid.NewGuid()}.json");
            try
            {
                var store = new LocalSaveStore(path);
                var save = store.Load();
                save.wallet.gold = 321;
                store.Flush(save);
                _credentials.Save(LoginMethod.Guest, "original-guest");
                var transport = new FakeHttpTransport();
                transport.Queued.Enqueue(HttpResponseData.Failed(HttpFailure.Timeout));
                transport.Queued.Enqueue(new HttpResponseData(retryStatus, null));
                var client = new HttpApiClient(new HttpApiConfig("https://example.test"), transport, tokens: _session);
                var api = new ServerBackedAccountApi(client, new LocalAccountApi(store));
                var service = new AccountService(api, new SessionAuthCredentialStore(_credentials, _session),
                    _nav, new[] { new FakeSocial(true, "google-id-token") });
                LogAssert.Expect(LogType.Exception, new Regex(HttpErrorMapper.Timeout));

                Assert.AreEqual(LinkResult.Failed, service.LinkAsync().GetAwaiter().GetResult());
                Assert.AreEqual(1, transport.Requests.Count, "응답 유실 직후 자동 재전송하지 않는다");
                Assert.AreEqual("original-guest", _credentials.AccountId);
                if (retryStatus == 409) { LogAssert.Expect(LogType.Exception, new Regex("HTTP_409")); }

                Assert.AreEqual(expected, service.LinkAsync().GetAwaiter().GetResult());
                Assert.AreEqual(expectedMethod, _credentials.Method);
                Assert.AreEqual(retryStatus == 204 ? AuthCredentials.SocialAccountId : "original-guest", _credentials.AccountId);
                Assert.AreEqual(2, transport.Requests.Count);
                Assert.AreEqual(transport.Requests[0].Body, transport.Requests[1].Body);
                Assert.AreEqual("Bearer access", transport.Requests[1].Headers["Authorization"]);
                Assert.AreEqual(321, store.Load().wallet.gold, "연동 재시도가 기존 진행을 초기화하면 안 된다");
                Assert.AreEqual(0, _nav.LoadingCalls);
            }
            finally { System.IO.File.Delete(path); }
        }
    }
}
