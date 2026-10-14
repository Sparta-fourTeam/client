using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Core;
using Game.Network;
using NUnit.Framework;

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
            public UniTask DeleteAccount() { Deleted++; return UniTask.CompletedTask; }
            public UniTask LinkAccount(LoginMethod method, string accountId) { Linked = method; return UniTask.CompletedTask; }
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
    }
}
