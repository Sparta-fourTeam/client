using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Core;
using Game.Network;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class ClientAccessPolicyTests
    {
        [TestCase(UnityEngine.RuntimePlatform.IPhonePlayer, "")]
        [TestCase(UnityEngine.RuntimePlatform.IPhonePlayer, "0")]
        [TestCase(UnityEngine.RuntimePlatform.Android, "0")]
        public void DevicePolicyStillFetchesMaintenanceWhenServerAuthenticationIsDisabled(UnityEngine.RuntimePlatform platform, string preference)
        {
            Assert.IsFalse(ServerConfig.ResolveServerAuth(platform, preference));
            var transport = new FakeHttpTransport
            {
                Response = new HttpResponseData(200, "{\"schemaVersion\":1,\"revision\":\"maintenance\",\"mode\":\"MAINTENANCE\",\"message\":\"test\",\"enforcementEnabled\":true,\"allowLegacy\":true,\"retryAfterSeconds\":30,\"platforms\":{}}")
            };
            var client = new HttpClientPolicyClient(new HttpApiConfig("https://example.test"), transport,
                ServerConfig.ResolveClientPolicy(platform));
            var policy = client.Fetch(default).GetAwaiter().GetResult();
            var platformKey = platform == UnityEngine.RuntimePlatform.IPhonePlayer ? "ios" : "android";
            Assert.AreEqual(1, transport.Requests.Count);
            Assert.AreEqual(ClientAccessState.Maintenance, policy.Evaluate(new ClientBuild(platformKey, "10"), ClientAccessPurpose.Login).State);
            policy.mode = "NORMAL";
            policy.platforms[platformKey] = new ClientPlatformPolicy
            { minimumBuild = "11", recommendedBuild = "11", storeUrl = platformKey == "ios" ? "https://apps.apple.com/app/id-test" : "https://play.google.com/store/apps/details?id=test" };
            Assert.AreEqual(ClientAccessState.UpdateRequired, policy.Evaluate(new ClientBuild(platformKey, "10"), ClientAccessPurpose.Login).State);
        }

        [TestCase("1", "1.0", 0)]
        [TestCase("1.10", "1.9", 1)]
        [TestCase("9", "10", -1)]
        public void BuildComparisonIsNumeric(string left, string right, int expected) =>
            Assert.That(Math.Sign(ClientAccessPolicy.CompareBuild(left, right)), Is.EqualTo(expected));

        [TestCase(null)]
        [TestCase("")]
        [TestCase("1-beta")]
        [TestCase("-1")]
        [TestCase("1.2.3.4.5")]
        [TestCase("10000000000")]
        [TestCase("1\n")]
        public void InvalidBuildIsRejected(string value) => Assert.IsFalse(ClientAccessPolicy.ValidBuild(value));

        [TestCase("10", ClientAccessState.Allowed)]
        [TestCase("11", ClientAccessState.Allowed)]
        [TestCase("9", ClientAccessState.UpdateRequired)]
        [TestCase("bad", ClientAccessState.UpdateRequired)]
        [TestCase(null, ClientAccessState.UpdateRequired)]
        public void MinimumBuildIsInclusive(string build, ClientAccessState expected) =>
            Assert.That(Policy("NORMAL", "10").Evaluate(new ClientBuild("android", build),
                ClientAccessPurpose.Login).State, Is.EqualTo(expected));

        [Test]
        public void OffPolicyKeepsLegacyAndInvalidMetadataCompatible()
        {
            var policy = Policy("MAINTENANCE", "10"); policy.enforcementEnabled = false;
            Assert.IsFalse(policy.Evaluate(new ClientBuild(null, null), ClientAccessPurpose.Login).Blocking);
        }

        [Test]
        public void DrainingAllowsExistingResumeButNotNewEntry()
        {
            var policy = Policy("DRAINING", "11"); var build = new ClientBuild("android", "10");
            Assert.IsTrue(policy.Evaluate(build, ClientAccessPurpose.Login).Blocking);
            Assert.IsTrue(policy.Evaluate(build, ClientAccessPurpose.NewBattle).Blocking);
            Assert.IsFalse(policy.Evaluate(build, ClientAccessPurpose.Resume).Blocking);
            policy.mode = "MAINTENANCE";
            Assert.IsTrue(policy.Evaluate(build, ClientAccessPurpose.Resume).Blocking);
        }

        [Test]
        public async Task PendingAndSupersededRepliesKeepInputBlockedUntilFreshPermission()
        {
            var first = new UniTaskCompletionSource<ClientAccessPolicy>();
            var second = new UniTaskCompletionSource<ClientAccessPolicy>();
            var client = new Client(); client.Replies.Enqueue(first); client.Replies.Enqueue(second);
            var prompt = new Prompt(); var guard = new ClientAccessGuard(client, prompt, new Advisory());
            Task entry = guard.EnsureAllowed(ClientAccessPurpose.Login).AsTask();
            Assert.IsTrue(prompt.Checking); Assert.IsFalse(entry.IsCompleted);
            guard.Recheck(); first.TrySetResult(Policy());
            Assert.IsFalse(entry.IsCompleted); Assert.IsTrue(prompt.Checking);
            Assert.IsTrue(prompt.CheckingChanges.TrueForAll(value => value));
            second.TrySetResult(Policy()); await entry;
            Assert.IsFalse(prompt.Checking); Assert.AreEqual(2, client.Fetches);
        }

        [Test]
        public async Task CancellingAnUnfinishedLookupNeverGrantsEntryOrLeavesBusyInputBlock()
        {
            using var cancellation = new CancellationTokenSource();
            var reply = new UniTaskCompletionSource<ClientAccessPolicy>();
            var client = new Client(); client.Replies.Enqueue(reply);
            var prompt = new Prompt(); var guard = new ClientAccessGuard(client, prompt, new Advisory());
            Task entry = guard.EnsureAllowed(ClientAccessPurpose.Login, cancellation.Token).AsTask();
            cancellation.Cancel();
            try { await entry; Assert.Fail("Cancellation must not grant entry."); }
            catch (OperationCanceledException) { }
            Assert.IsFalse(prompt.Checking); Assert.IsFalse(guard.IsChecking);
            reply.TrySetResult(Policy());
        }

        [Test]
        public async Task AdvisoryMayBeSkippedWithoutRepeatedStorePrompts()
        {
            var client = new Client(); client.Replies.Enqueue(Policy()); client.Replies.Enqueue(Policy());
            var prompt = new Prompt(); var advisory = new Advisory { Offer = true };
            var guard = new ClientAccessGuard(client, prompt, advisory);
            await guard.EnsureAllowed(ClientAccessPurpose.Login);
            await guard.EnsureAllowed(ClientAccessPurpose.Login);
            Assert.AreEqual(1, prompt.Seen.Count); Assert.AreEqual(0, prompt.StoreOpened);
            Assert.AreEqual(1, advisory.Calls);
        }

        [Test]
        public async Task ClosingOrOpeningStoreNeverGrantsRequiredUpdate()
        {
            var client = new Client();
            client.Replies.Enqueue(Policy("NORMAL", "11"));
            client.Replies.Enqueue(Policy("NORMAL", "11"));
            client.Replies.Enqueue(Policy());
            var prompt = new Prompt(); prompt.Answers.Enqueue(false); prompt.Answers.Enqueue(true);
            await new ClientAccessGuard(client, prompt, new Advisory()).EnsureAllowed(ClientAccessPurpose.Login);
            Assert.AreEqual(3, client.Fetches); Assert.AreEqual(2, prompt.Seen.Count);
            Assert.AreEqual(1, prompt.StoreOpened);
        }

        [TestCase("NETWORK_ERROR")]
        [TestCase("TIMEOUT")]
        public async Task PolicyFailureBlocksUntilSuccessfulRetry(string code)
        {
            var client = new Client(); client.Replies.Enqueue(new ApiException(ApiErrorKind.Transient, code));
            client.Replies.Enqueue(Policy()); var prompt = new Prompt(); var advisory = new Advisory();
            prompt.BeforeReply = () => Assert.AreEqual(0, advisory.Calls);
            await new ClientAccessGuard(client, prompt, advisory).EnsureAllowed(ClientAccessPurpose.Login);
            Assert.AreEqual(ClientAccessState.Unavailable, prompt.Seen[0].State);
            Assert.AreEqual(2, client.Fetches);
        }

        [Test]
        public async Task ForegroundRecheckDoesNotTreatModalCancellationAsPermission()
        {
            var client = new Client(); client.Replies.Enqueue(Policy("MAINTENANCE")); client.Replies.Enqueue(Policy());
            var prompt = new Prompt { Wait = new UniTaskCompletionSource<bool>() };
            var guard = new ClientAccessGuard(client, prompt, new Advisory());
            Task task = guard.EnsureAllowed(ClientAccessPurpose.Resume).AsTask();
            Assert.IsFalse(task.IsCompleted);
            guard.Recheck(); await task;
            Assert.AreEqual(2, client.Fetches);
        }

        [Test]
        public async Task ForegroundDiscardsAnOldInFlightNormalResponse()
        {
            var client = new Client(); var pending = new UniTaskCompletionSource<ClientAccessPolicy>();
            client.Replies.Enqueue(pending); client.Replies.Enqueue(Policy("MAINTENANCE")); client.Replies.Enqueue(Policy());
            var prompt = new Prompt();
            var guard = new ClientAccessGuard(client, prompt, new Advisory());
            Task task = guard.EnsureAllowed(ClientAccessPurpose.Resume).AsTask();
            guard.Recheck();
            prompt.Wait = new UniTaskCompletionSource<bool>();
            pending.TrySetResult(Policy());
            Assert.IsFalse(task.IsCompleted); Assert.AreEqual(2, client.Fetches);
            guard.Recheck(); await task;
            Assert.AreEqual(3, client.Fetches);
        }

        [Test]
        public async Task ForegroundDuringAdvisoryCannotReleaseStalePermission()
        {
            var client = new Client(); client.Replies.Enqueue(Policy());
            client.Replies.Enqueue(Policy("MAINTENANCE")); client.Replies.Enqueue(Policy());
            var prompt = new Prompt { Wait = new UniTaskCompletionSource<bool>() };
            var guard = new ClientAccessGuard(client, prompt, new Advisory { Offer = true });
            Task task = guard.EnsureAllowed(ClientAccessPurpose.Login).AsTask();
            Assert.IsFalse(task.IsCompleted);
            guard.Recheck();
            await task;
            Assert.AreEqual(3, client.Fetches);
            Assert.AreEqual(ClientAccessState.Maintenance, prompt.Seen[1].State);
        }

        [Test]
        public void CancellationAfterUpdateClickCannotOpenStoreOrGrantEntry()
        {
            using var cancel = new CancellationTokenSource();
            var client = new Client(); client.Replies.Enqueue(Policy("NORMAL", "11"));
            var prompt = new Prompt { BeforeReply = cancel.Cancel }; prompt.Answers.Enqueue(true);
            var guard = new ClientAccessGuard(client, prompt, new Advisory());
            Assert.Throws<OperationCanceledException>(() => guard.EnsureAllowed(ClientAccessPurpose.Login, cancel.Token).GetAwaiter().GetResult());
            Assert.AreEqual(0, prompt.StoreOpened); Assert.IsFalse(guard.IsChecking);
        }

        [Test]
        public void PolicyErrorsPreserveRetrySemanticsAndRefreshSession()
        {
            var error = HttpErrorMapper.Map(new HttpResponseData(426, "{\"code\":\"CLIENT_UPDATE_REQUIRED\"}"));
            Assert.IsFalse(HttpErrorMapper.IsDefinitiveRequestRejection(error));
            var session = new AuthSession(); session.Set("old-access", "old-refresh");
            var transport = new FakeHttpTransport();
            transport.Queued.Enqueue(new HttpResponseData(401, "{}"));
            transport.Queued.Enqueue(new HttpResponseData(503, "{\"code\":\"MAINTENANCE\"}"));
            var api = new HttpApiClient(new HttpApiConfig("https://example.test"), transport, tokens: session);
            var ex = Assert.Throws<ApiException>(() => api.Get<object>("api/player/me").GetAwaiter().GetResult());
            Assert.AreEqual("MAINTENANCE", ex.Code); Assert.AreEqual("old-refresh", session.RefreshToken);
            Assert.IsTrue(transport.Requests[1].Headers.ContainsKey("X-Game-Build"));
            Assert.IsFalse(transport.Requests[1].Headers.ContainsKey("Authorization"));
        }

        [Test]
        public void PolicyRevisionCannotInjectHeadersOrDivergeFromServerSchema()
        {
            foreach (string revision in new[] { "bad revision", "bad\r\nheader", "" })
            {
                var policy = Policy(); policy.revision = revision;
                Assert.Throws<FormatException>(() => HttpClientPolicyClient.Validate(policy));
            }
        }

        [Test]
        public void OldServer404HasAdvisoryFallbackButTimeoutAndLater404DoNot()
        {
            var transport = new FakeHttpTransport();
            var client = new HttpClientPolicyClient(new HttpApiConfig("https://example.test"), transport);
            transport.Response = new HttpResponseData(404, "");
            Assert.IsFalse(client.Fetch(default).GetAwaiter().GetResult().enforcementEnabled);
            transport.Response = HttpResponseData.Failed(HttpFailure.Timeout);
            Assert.Throws<ApiException>(() => client.Fetch(default).GetAwaiter().GetResult());
            transport.Response = new HttpResponseData(200, "{\"schemaVersion\":1,\"revision\":\"off\",\"mode\":\"NORMAL\",\"message\":\"\",\"enforcementEnabled\":false,\"allowLegacy\":true,\"retryAfterSeconds\":30,\"platforms\":{}}");
            Assert.IsFalse(client.Fetch(default).GetAwaiter().GetResult().enforcementEnabled);
            transport.Response = new HttpResponseData(404, "");
            Assert.Throws<ApiException>(() => client.Fetch(default).GetAwaiter().GetResult());
        }

        private static ClientAccessPolicy Policy(string mode = "NORMAL", string minimum = "0") => new ClientAccessPolicy
        {
            schemaVersion = 1,
            revision = "test",
            enforcementEnabled = true,
            mode = mode,
            message = "",
            retryAfterSeconds = 30,
            platforms = new Dictionary<string, ClientPlatformPolicy>
            {
                ["android"] = new ClientPlatformPolicy
                {
                    minimumBuild = minimum,
                    recommendedBuild = minimum,
                    storeUrl = "https://play.google.com/store/apps/details?id=test"
                }
            },
        };

        private sealed class Client : IClientPolicyClient
        {
            public readonly Queue<object> Replies = new(); public int Fetches;
            public ClientBuild Build => new ClientBuild("android", "10");
            public UniTask<ClientAccessPolicy> Fetch(CancellationToken cancellationToken)
            {
                Fetches++; var value = Replies.Dequeue();
                if (value is UniTaskCompletionSource<ClientAccessPolicy> pending) { return pending.Task; }
                if (value is Exception exception) { throw exception; }
                return UniTask.FromResult((ClientAccessPolicy)value);
            }
        }

        private sealed class Prompt : IClientAccessPrompt
        {
            public bool Checking;
            public readonly List<bool> CheckingChanges = new();
            public void SetChecking(bool checking) { Checking = checking; CheckingChanges.Add(checking); }
            public readonly List<ClientAccessDecision> Seen = new(); public readonly Queue<bool> Answers = new();
            public int StoreOpened; public Action BeforeReply; public UniTaskCompletionSource<bool> Wait;
            public UniTask<bool> Show(ClientAccessDecision decision, CancellationToken cancellationToken)
            {
                Seen.Add(decision); BeforeReply?.Invoke();
                return Wait != null ? Wait.Task : UniTask.FromResult(Answers.Count > 0 && Answers.Dequeue());
            }
            public void OpenStore(string url) => StoreOpened++;
            public void Recheck() { var pending = Wait; Wait = null; pending?.TrySetResult(false); }
        }

        private sealed class Advisory : IAppUpdateChecker
        {
            public bool Offer; public int Calls;
            public UniTask<AppUpdateInfo> CheckAsync(CancellationToken cancellationToken)
            { Calls++; return UniTask.FromResult(Offer ? new AppUpdateInfo("https://play.google.com/store/apps/details?id=test") : null); }
        }
    }
}
