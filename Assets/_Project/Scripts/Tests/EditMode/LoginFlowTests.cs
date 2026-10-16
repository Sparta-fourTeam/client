using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using Game.Core;
using Game.Network;
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
            public LoginMethod LastMethod;
            public string LastAccountId;       // 마지막 호출에 받은 계정 ID
            public string ReturnAccountId;     // 서버가 정해 주는 계정 ID (null이면 정해 주지 않는다)

            public UniTask<LoginResponse> Login(LoginMethod method, string accountId)
            {
                Calls++;
                LastMethod = method;
                LastAccountId = accountId;
                if (Calls <= FailFirst) { throw new InvalidOperationException("로그인 실패 (테스트)"); }
                return UniTask.FromResult(new LoginResponse { accessToken = "test", accountId = ReturnAccountId });
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
            public LoginConsent Consent = new(true, false, false);
            public UniTaskCompletionSource<LoginConsent> PendingConsent;
            public UniTask<LoginConsent> WaitForConsentAsync()
            {
                Calls.Add("consent");
                return PendingConsent != null ? PendingConsent.Task : UniTask.FromResult(Consent);
            }
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
            public int SilentCalls; // SignInSilentAsync로 들어온 횟수. Calls에도 함께 센다

            public UniTask<string> SignInAsync()
            {
                Calls++;
                return UniTask.FromResult(AccountId);
            }

            public UniTask<string> SignInSilentAsync()
            {
                SilentCalls++;
                return SignInAsync();
            }
        }

        private sealed class RecordingCollection : IAnalyticsCollectionControl
        {
            public readonly List<bool> Choices = new();
            public bool Allowed;
            public void SetCollectionAllowed(bool allowed) { Choices.Add(allowed); Allowed = allowed; }
        }

        private string _consentKey;
        private PlayerPrefsLoginConsentStore _consents;
        private RecordingCollection _analytics;
        private FakeAuthApi _auth;
        private PlayerProfile _profile;
        private MemoryCredentialStore _credentials;
        private FakePrompt _prompt;

        [SetUp]
        public void SetUp()
        {
            _consentKey = "test.consent." + Guid.NewGuid().ToString("N");
            _consents = new PlayerPrefsLoginConsentStore(_consentKey);
            _analytics = new RecordingCollection();
            _auth = new FakeAuthApi();
            _profile = new PlayerProfile(new GameDataStore());
            _credentials = new MemoryCredentialStore();
            _prompt = new FakePrompt();
        }

        private LoginFlow Flow(params ISocialSignIn[] social) =>
            new LoginFlow(_auth, new FakePlayerApi(), _profile, _credentials, _prompt, social, _analytics, _consents);

        [TearDown]
        public void ClearTestConsent() => _consents.Clear();

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
            CollectionAssert.AreEqual(new[] { "consent", "signing", "hide" }, _prompt.Calls);
            Assert.AreEqual(1, _auth.Calls);
            Assert.AreEqual(77, _profile.Gold);
            Assert.AreEqual("saved", _credentials.AccountId, "기존 로그인 정보는 그대로 둔다");
        }

        [Test(Description = "최초 진입이면 로그인창에서 게스트를 고른 뒤 로그인하고, 성공하면 게스트 로그인 정보를 저장한다")]
        public void WithoutCredential_GuestSignInSavesCredential()
        {
            bool prompted = Flow().RunAsync().GetAwaiter().GetResult();

            Assert.IsTrue(prompted, "로그인창에서 버튼을 눌렀으므로 터치를 다시 기다리지 않는다");
            CollectionAssert.AreEqual(new[] { "consent", "signIn", "signing", "hide" }, _prompt.Calls);
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
            Assert.AreEqual(AuthCredentials.SocialAccountId, _credentials.AccountId, "ID 토큰은 기기에 저장하지 않는다");
        }

        [Test(Description = "외부 계정 로그인을 취소하면 서버 로그인 없이 로그인창으로 돌아간다")]
        public void SocialCancelled_ReturnsToPrompt()
        {
            var google = new FakeSocial(LoginMethod.Google, available: true, accountId: null);
            _prompt.Choices.Enqueue(LoginMethod.Google);
            _prompt.Choices.Enqueue(LoginMethod.Guest);

            Flow(google).RunAsync().GetAwaiter().GetResult();

            CollectionAssert.AreEqual(new[] { "consent", "signIn", "signIn", "signing", "hide" }, _prompt.Calls);
            Assert.AreEqual(1, _auth.Calls, "취소한 동안에는 서버에 로그인하지 않는다");
            Assert.AreEqual(LoginMethod.Guest, _credentials.Method);
        }

        [Test(Description = "저장된 방식을 지금 쓸 수 없으면(외부 계정 SDK가 없는 빌드 등) 로그인창을 다시 띄운다")]
        public void SavedSocialUnavailable_ShowsPrompt()
        {
            _credentials.Save(LoginMethod.Apple, "apple-account");

            Flow(new FakeSocial(LoginMethod.Apple, available: false, "apple-account")).RunAsync().GetAwaiter().GetResult();

            Assert.AreEqual("signIn", _prompt.Calls[1]);
            Assert.AreEqual(LoginMethod.Guest, _credentials.Method, "새로 고른 방식으로 바꿔 저장한다");
        }

        [Test(Description = "로그인에 실패하면 [다시 시도]를 기다렸다가 다시 로그인하고, 실패한 동안에는 로그인 정보를 저장하지 않는다")]
        public void Failure_WaitsForRetryAndSavesOnlyAfterSuccess()
        {
            _auth.FailFirst = 2;
            ExpectLoginFailureLog(2);
            var store = _credentials;

            Flow().RunAsync().GetAwaiter().GetResult();

            CollectionAssert.AreEqual(new[] { "consent", "signIn", "signing", "retry", "signing", "retry", "signing", "hide" }, _prompt.Calls);
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
            Assert.AreEqual(AuthCredentials.SocialAccountId, _credentials.AccountId);
        }

        [Test(Description = "로그인 정보가 있어도 로그인에 실패하면 로비로 넘어가지 않고 재시도를 기다린다")]
        public void WithCredential_FailureAlsoRetries()
        {
            _credentials.Save(LoginMethod.Guest, "saved");
            _auth.FailFirst = 1;
            ExpectLoginFailureLog(1);

            Flow().RunAsync().GetAwaiter().GetResult();

            CollectionAssert.AreEqual(new[] { "consent", "signing", "retry", "signing", "hide" }, _prompt.Calls);
            Assert.AreEqual("saved", _credentials.AccountId);
        }

        [Test(Description = "저장된 방식이 구글이면 저장된 값을 쓰지 않고 구글 로그인을 다시 해서, 새로 받은 토큰으로 서버에 로그인한다")]
        public void SavedGoogle_SignsInAgainAndSendsFreshToken()
        {
            _credentials.Save(LoginMethod.Google, "stale-token");
            var google = new FakeSocial(LoginMethod.Google, available: true, "fresh-token");

            bool prompted = Flow(google).RunAsync().GetAwaiter().GetResult();

            Assert.IsFalse(prompted, "저장된 방식이 있으니 로그인창은 띄우지 않는다");
            Assert.AreEqual(1, google.Calls);
            Assert.AreEqual(LoginMethod.Google, _auth.LastMethod);
            Assert.AreEqual("fresh-token", _auth.LastAccountId);
        }

        [Test(Description = "저장된 방식이 구글이어도 저장 값은 바뀌지 않고, 서버에는 새로 받은 토큰을 보낸다 (토큰을 기기에 남기지 않는다)")]
        public void SavedGoogle_KeepsMarkerAndNeverStoresToken()
        {
            _credentials.Save(LoginMethod.Google, AuthCredentials.SocialAccountId);
            var google = new FakeSocial(LoginMethod.Google, available: true, "fresh-token");

            Flow(google).RunAsync().GetAwaiter().GetResult();

            Assert.AreEqual("fresh-token", _auth.LastAccountId);
            Assert.AreEqual(AuthCredentials.SocialAccountId, _credentials.AccountId);
        }

        [Test(Description = "저장된 방식으로 바로 들어가는 구글 로그인은 계정 선택 없이 시도한다")]
        public void SavedGoogle_SignsInSilently()
        {
            _credentials.Save(LoginMethod.Google, "stale-token");
            var google = new FakeSocial(LoginMethod.Google, available: true, "fresh-token");

            Flow(google).RunAsync().GetAwaiter().GetResult();

            Assert.AreEqual(1, google.SilentCalls, "앱을 켤 때마다 계정 선택창이 뜨지 않게 한다");
        }

        [Test(Description = "로그인창에서 사용자가 고른 구글 로그인은 계정 선택창을 보여 준다")]
        public void PromptedGoogle_IsNotSilent()
        {
            var google = new FakeSocial(LoginMethod.Google, available: true, "google-account");
            _prompt.Choices.Enqueue(LoginMethod.Google);

            Flow(google).RunAsync().GetAwaiter().GetResult();

            Assert.AreEqual(1, google.Calls);
            Assert.AreEqual(0, google.SilentCalls);
        }

        [Test(Description = "저장된 게스트 ID는 그대로 서버에 보내고, 서버가 새 게스트 ID를 정해 주면 그 값으로 바꿔 저장한다")]
        public void Guest_SendsSavedIdAndSavesIdFromServer()
        {
            _credentials.Save(LoginMethod.Guest, "old-guest-id");
            _auth.ReturnAccountId = "new-guest-id";

            Flow().RunAsync().GetAwaiter().GetResult();

            Assert.AreEqual("old-guest-id", _auth.LastAccountId);
            Assert.AreEqual("new-guest-id", _credentials.AccountId);
            Assert.AreEqual(LoginMethod.Guest, _credentials.Method);
        }

        [Test(Description = "처음 게스트로 들어오면 계정 ID 없이 서버에 로그인하고, 서버가 정해 준 게스트 ID를 저장한다")]
        public void NewGuest_SendsNoIdAndSavesIdFromServer()
        {
            _auth.ReturnAccountId = "server-guest-id";

            Flow().RunAsync().GetAwaiter().GetResult();

            Assert.IsNull(_auth.LastAccountId);
            Assert.AreEqual("server-guest-id", _credentials.AccountId);
        }

        [Test(Description = "로그인창을 기다리는 중에 취소되면(씬 전환 등) 로그인하지 않고 취소를 그대로 전한다")]
        public void Cancelled_DoesNotSignIn()
        {
            var flow = new LoginFlow(_auth, new FakePlayerApi(), _profile, _credentials, new CancellingPrompt(), Array.Empty<ISocialSignIn>());

            Assert.Throws<OperationCanceledException>(() => flow.RunAsync().GetAwaiter().GetResult());
            Assert.AreEqual(0, _auth.Calls);
            Assert.IsFalse(_credentials.HasCredential);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ServiceConsentRejected_DoesNotAuthenticateOrChangeCredentials(bool savedGoogle)
        {
            if (savedGoogle) { _credentials.Save(LoginMethod.Google, AuthCredentials.SocialAccountId); }
            _prompt.Consent = default;
            var google = new FakeSocial(LoginMethod.Google, true, "unused-token");

            Assert.Throws<OperationCanceledException>(() => Flow(google).RunAsync().GetAwaiter().GetResult());

            Assert.AreEqual(0, _auth.Calls);
            Assert.AreEqual(0, google.Calls);
            Assert.AreEqual(savedGoogle, _credentials.HasCredential);
            Assert.IsFalse(_profile.IsLoaded);
            CollectionAssert.AreEqual(new[] { "consent" }, _prompt.Calls);
        }

        [Test]
        public void ServiceConsentPending_BlocksSavedGuestUntilAccepted()
        {
            _credentials.Save(LoginMethod.Guest, "saved");
            _prompt.PendingConsent = new UniTaskCompletionSource<LoginConsent>();
            var pending = Flow().RunAsync();

            Assert.AreEqual(0, _auth.Calls);
            Assert.IsFalse(_profile.IsLoaded);
            _prompt.PendingConsent.TrySetResult(new LoginConsent(true, false, false));
            pending.GetAwaiter().GetResult();
            Assert.AreEqual(1, _auth.Calls);
            Assert.AreEqual("saved", _credentials.AccountId);
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void AnalyticsChoice_DoesNotBlockGameAndRequiresBothChoices(bool collection, bool transfer)
        {
            bool granted = collection && transfer;
            _prompt.Consent = new LoginConsent(true, collection, transfer);
            Flow().RunAsync().GetAwaiter().GetResult();
            Assert.AreEqual(1, _auth.Calls);
            Assert.AreEqual(77, _profile.Gold);
            Assert.AreEqual(granted, _analytics.Allowed);
            CollectionAssert.AreEqual(new[] { false, granted }, _analytics.Choices);
        }

        [Test]
        public void AnalyticsChoicePending_DoesNotInitializeOrAuthenticate()
        {
            _credentials.Save(LoginMethod.Google, AuthCredentials.SocialAccountId);
            var google = new FakeSocial(LoginMethod.Google, true, "unused");
            _prompt.PendingConsent = new UniTaskCompletionSource<LoginConsent>();
            var pending = Flow(google).RunAsync();
            Assert.AreEqual(0, _auth.Calls);
            Assert.AreEqual(0, google.Calls);
            CollectionAssert.AreEqual(new[] { false }, _analytics.Choices);
            _prompt.PendingConsent.TrySetResult(new LoginConsent(true, false, false));
            pending.GetAwaiter().GetResult();
            Assert.AreEqual(1, _auth.Calls);
            Assert.IsFalse(_analytics.Allowed);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SavedChoice_SkipsPromptAfterRecreatingStoreIncludingDecline(bool granted)
        {
            _prompt.Consent = new LoginConsent(true, granted, granted);
            Flow().RunAsync().GetAwaiter().GetResult();
            _consents = new PlayerPrefsLoginConsentStore(_consentKey);
            _prompt.Consent = default; // 다시 물으면 거절될 설정이어도 저장된 선택을 사용한다.
            Flow().RunAsync().GetAwaiter().GetResult();
            Assert.AreEqual(1, _prompt.Calls.Count(c => c == "consent"));
            Assert.AreEqual(2, _auth.Calls);
            Assert.AreEqual(granted, _analytics.Allowed);
        }

        [TestCase("not-json")]
        [TestCase("{\"revision\":\"old\",\"termsAccepted\":true,\"analyticsAccepted\":true,\"transferAccepted\":true}")]
        public void InvalidOrOldSavedChoice_DoesNotEnableAnalyticsBeforeNewChoice(string saved)
        {
            PlayerPrefs.SetString(_consentKey, saved);
            _prompt.PendingConsent = new UniTaskCompletionSource<LoginConsent>();
            var pending = Flow().RunAsync();
            Assert.AreEqual(0, _auth.Calls);
            Assert.IsFalse(_analytics.Allowed);
            _prompt.PendingConsent.TrySetResult(new LoginConsent(true, false, false));
            pending.GetAwaiter().GetResult();
            Assert.IsFalse(_analytics.Allowed);
        }

        private sealed class CancellingPrompt : ILoginPrompt
        {
            public UniTask<LoginConsent> WaitForConsentAsync() => UniTask.FromResult(new LoginConsent(true, false, false));
            public UniTask<LoginMethod> WaitForSignInAsync(IReadOnlyCollection<LoginMethod> available) => UniTask.FromCanceled<LoginMethod>();
            public UniTask WaitForRetryAsync() => UniTask.FromCanceled();
            public void ShowSigningIn() { }
            public void Hide() { }
        }
    }
}
