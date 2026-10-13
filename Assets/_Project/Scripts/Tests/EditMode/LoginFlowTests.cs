using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using Game.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests
{
    /// <summary>로비 진입 전 로그인 확인: 최초 진입이면 로그인창에서 방식을 고르고, 외부 계정 취소·로그인 실패를 처리하며,
    /// 성공해야 로그인 정보(방식과 계정 ID)를 저장하는지 본다</summary>
    public sealed class LoginFlowTests
    {
        private sealed class FakeAuthApi : IAuthApi
        {
            public int Calls;
            public int FailFirst;   // 처음 몇 번을 실패시킬지

            public UniTask<LoginResponse> Login()
            {
                Calls++;
                if (Calls <= FailFirst) { throw new InvalidOperationException("로그인 실패 (테스트)"); }
                return UniTask.FromResult(new LoginResponse { accessToken = "test" });
            }
        }

        private sealed class FakePlayerApi : IPlayerApi
        {
            public UniTask<PlayerSnapshot> GetMe() => UniTask.FromResult(new PlayerSnapshot { gold = 77 });
        }

        private sealed class MemoryCredentialStore : IAuthCredentialStore
        {
            public LoginMethod Method { get; private set; }
            public string AccountId { get; private set; }
            public bool HasCredential => AccountId != null;
            public void Save(LoginMethod method, string accountId) { Method = method; AccountId = accountId; }
            public void Clear() => AccountId = null;
        }

        // 사용자가 버튼을 바로 누른 것처럼 즉시 끝나고, 호출 순서와 제시된 방식을 기록한다
        private sealed class FakePrompt : ILoginPrompt
        {
            public readonly List<string> Calls = new();
            public readonly Queue<LoginMethod> Choices = new();     // 로그인창에서 차례로 고를 방식 (비면 게스트)
            public readonly List<LoginMethod[]> Offered = new();

            public UniTask<LoginMethod> WaitForSignInAsync(IReadOnlyCollection<LoginMethod> available)
            {
                Calls.Add("signIn");
                Offered.Add(available.ToArray());
                return UniTask.FromResult(Choices.Count > 0 ? Choices.Dequeue() : LoginMethod.Guest);
            }

            public UniTask WaitForRetryAsync() { Calls.Add("retry"); return UniTask.CompletedTask; }
            public void ShowSigningIn() => Calls.Add("signing");
            public void Hide() => Calls.Add("hide");
        }

        private sealed class FakeSocial : ISocialSignIn
        {
            public FakeSocial(LoginMethod method, bool available, string accountId)
            {
                Method = method;
                IsAvailable = available;
                AccountId = accountId;
            }

            public LoginMethod Method { get; }
            public bool IsAvailable { get; }
            public string AccountId;    // null이면 사용자가 취소한 것으로 본다
            public int Calls;

            public UniTask<string> SignInAsync()
            {
                Calls++;
                return UniTask.FromResult(AccountId);
            }
        }

        private FakeAuthApi _auth;
        private PlayerProfile _profile;
        private MemoryCredentialStore _credentials;
        private FakePrompt _prompt;

        [SetUp]
        public void SetUp()
        {
            _auth = new FakeAuthApi();
            _profile = new PlayerProfile(new GameDataStore());
            _credentials = new MemoryCredentialStore();
            _prompt = new FakePrompt();
        }

        private LoginFlow Flow(params ISocialSignIn[] social) =>
            new LoginFlow(_auth, new FakePlayerApi(), _profile, _credentials, _prompt, social);

        private static void ExpectLoginFailureLog(int times)
        {
            for (int i = 0; i < times; i++) { LogAssert.Expect(LogType.Exception, new Regex("로그인 실패")); }
        }

        [Test(Description = "저장된 게스트 로그인 정보가 있으면 로그인창 없이 바로 로그인하고 프로필을 반영한다")]
        public void WithCredential_SignsInWithoutPrompt()
        {
            _credentials.Save(LoginMethod.Guest, "saved");

            bool prompted = Flow().RunAsync().GetAwaiter().GetResult();

            Assert.IsFalse(prompted, "로그인창을 띄우지 않았으므로 Loading에서 터치를 기다린다");
            CollectionAssert.AreEqual(new[] { "signing", "hide" }, _prompt.Calls);
            Assert.AreEqual(1, _auth.Calls);
            Assert.AreEqual(77, _profile.Gold);
            Assert.AreEqual("saved", _credentials.AccountId, "기존 로그인 정보는 그대로 둔다");
        }

        [Test(Description = "최초 진입이면 로그인창에서 게스트를 고른 뒤 로그인하고, 성공하면 게스트 로그인 정보를 저장한다")]
        public void WithoutCredential_GuestSignInSavesCredential()
        {
            bool prompted = Flow().RunAsync().GetAwaiter().GetResult();

            Assert.IsTrue(prompted, "로그인창에서 버튼을 눌렀으므로 터치를 다시 기다리지 않는다");
            CollectionAssert.AreEqual(new[] { "signIn", "signing", "hide" }, _prompt.Calls);
            Assert.AreEqual(1, _auth.Calls);
            Assert.AreEqual(LoginMethod.Guest, _credentials.Method);
            Assert.IsTrue(_credentials.HasCredential);
            Assert.AreEqual(77, _profile.Gold);
        }

        [Test(Description = "쓸 수 없는 외부 계정은 로그인창에 제시하지 않는다 (게스트는 항상 제시)")]
        public void UnavailableSocial_IsNotOffered()
        {
            Flow(new FakeSocial(LoginMethod.Google, available: true, "g"), new FakeSocial(LoginMethod.Apple, available: false, "a"))
                .RunAsync().GetAwaiter().GetResult();

            CollectionAssert.AreEqual(new[] { LoginMethod.Guest, LoginMethod.Google }, _prompt.Offered[0]);
        }

        [Test(Description = "구글을 고르면 외부 계정으로 로그인한 뒤 서버 로그인을 하고, 구글 방식과 계정 ID를 저장한다")]
        public void Google_SignsInAndSavesMethod()
        {
            var google = new FakeSocial(LoginMethod.Google, available: true, "google-account");
            _prompt.Choices.Enqueue(LoginMethod.Google);

            Flow(google).RunAsync().GetAwaiter().GetResult();

            Assert.AreEqual(1, google.Calls);
            Assert.AreEqual(1, _auth.Calls);
            Assert.AreEqual(LoginMethod.Google, _credentials.Method);
            Assert.AreEqual("google-account", _credentials.AccountId);
        }

        [Test(Description = "외부 계정 로그인을 취소하면 서버 로그인 없이 로그인창으로 돌아간다")]
        public void SocialCancelled_ReturnsToPrompt()
        {
            var google = new FakeSocial(LoginMethod.Google, available: true, accountId: null);
            _prompt.Choices.Enqueue(LoginMethod.Google);
            _prompt.Choices.Enqueue(LoginMethod.Guest);

            Flow(google).RunAsync().GetAwaiter().GetResult();

            CollectionAssert.AreEqual(new[] { "signIn", "signIn", "signing", "hide" }, _prompt.Calls);
            Assert.AreEqual(1, _auth.Calls, "취소한 동안에는 서버에 로그인하지 않는다");
            Assert.AreEqual(LoginMethod.Guest, _credentials.Method);
        }

        [Test(Description = "저장된 방식을 지금 쓸 수 없으면(외부 계정 SDK가 없는 빌드 등) 로그인창을 다시 띄운다")]
        public void SavedSocialUnavailable_ShowsPrompt()
        {
            _credentials.Save(LoginMethod.Apple, "apple-account");

            Flow(new FakeSocial(LoginMethod.Apple, available: false, "apple-account")).RunAsync().GetAwaiter().GetResult();

            Assert.AreEqual("signIn", _prompt.Calls[0]);
            Assert.AreEqual(LoginMethod.Guest, _credentials.Method, "새로 고른 방식으로 바꿔 저장한다");
        }

        [Test(Description = "로그인에 실패하면 [다시 시도]를 기다렸다가 다시 로그인하고, 실패한 동안에는 로그인 정보를 저장하지 않는다")]
        public void Failure_WaitsForRetryAndSavesOnlyAfterSuccess()
        {
            _auth.FailFirst = 2;
            ExpectLoginFailureLog(2);
            var store = _credentials;

            Flow().RunAsync().GetAwaiter().GetResult();

            CollectionAssert.AreEqual(new[] { "signIn", "signing", "retry", "signing", "retry", "signing", "hide" }, _prompt.Calls);
            Assert.AreEqual(3, _auth.Calls);
            Assert.IsTrue(store.HasCredential);
        }

        [Test(Description = "외부 계정으로 로그인한 뒤 서버 로그인이 실패하면, 재시도 때 외부 계정 로그인을 다시 묻지 않는다")]
        public void SocialThenServerFailure_RetriesServerOnly()
        {
            var google = new FakeSocial(LoginMethod.Google, available: true, "google-account");
            _prompt.Choices.Enqueue(LoginMethod.Google);
            _auth.FailFirst = 1;
            ExpectLoginFailureLog(1);

            Flow(google).RunAsync().GetAwaiter().GetResult();

            Assert.AreEqual(1, google.Calls);
            Assert.AreEqual(2, _auth.Calls);
            Assert.AreEqual("google-account", _credentials.AccountId);
        }

        [Test(Description = "로그인 정보가 있어도 로그인에 실패하면 로비로 넘어가지 않고 재시도를 기다린다")]
        public void WithCredential_FailureAlsoRetries()
        {
            _credentials.Save(LoginMethod.Guest, "saved");
            _auth.FailFirst = 1;
            ExpectLoginFailureLog(1);

            Flow().RunAsync().GetAwaiter().GetResult();

            CollectionAssert.AreEqual(new[] { "signing", "retry", "signing", "hide" }, _prompt.Calls);
            Assert.AreEqual("saved", _credentials.AccountId);
        }

        [Test(Description = "로그인창을 기다리는 중에 취소되면(씬 전환 등) 로그인하지 않고 취소를 그대로 전한다")]
        public void Cancelled_DoesNotSignIn()
        {
            var flow = new LoginFlow(_auth, new FakePlayerApi(), _profile, _credentials, new CancellingPrompt(), Array.Empty<ISocialSignIn>());

            Assert.Throws<OperationCanceledException>(() => flow.RunAsync().GetAwaiter().GetResult());
            Assert.AreEqual(0, _auth.Calls);
            Assert.IsFalse(_credentials.HasCredential);
        }

        private sealed class CancellingPrompt : ILoginPrompt
        {
            public UniTask<LoginMethod> WaitForSignInAsync(IReadOnlyCollection<LoginMethod> available) => UniTask.FromCanceled<LoginMethod>();
            public UniTask WaitForRetryAsync() => UniTask.FromCanceled();
            public void ShowSigningIn() { }
            public void Hide() { }
        }
    }
}
