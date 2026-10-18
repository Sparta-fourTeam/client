using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;
using Game.Network;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>수령 영수증과 현재 상태를 혼동하거나 재시도에서 계정·기간을 바꾸는 회귀를 막는다.</summary>
    public sealed class HttpAccountProgressTests
    {
        public const string At = "2026-10-18T20:21:22.123456Z";
        public const string Boards = "[{\"period\":\"DAILY\",\"periodKey\":\"20261019\",\"nextResetAt\":\"2026-10-19T19:00:00Z\","
            + "\"lastLoginDay\":\"20261019\",\"loginCount\":1,\"spentEnergy\":5,\"points\":13,"
            + "\"quests\":[{\"id\":\"daily.login\",\"kind\":\"LOGIN\",\"count\":1,\"target\":1,\"points\":13,\"claimed\":true,\"claimedAt\":\"" + At + "\"}],"
            + "\"chests\":[{\"id\":\"daily.chest\",\"points\":13,\"claimed\":true,\"claimedAt\":\"" + At + "\"}]},"
            + "{\"period\":\"WEEKLY\",\"periodKey\":\"20261019\",\"nextResetAt\":\"2026-10-25T19:00:00Z\",\"lastLoginDay\":null,\"loginCount\":0,\"spentEnergy\":0,\"points\":0,\"quests\":[],\"chests\":[]}]";
        public const string States = "[{\"tutorialId\":\"intro_battle\",\"dialogueSeen\":true,\"completed\":false,\"rewardClaimed\":false,"
            + "\"dialogueSeenAt\":\"" + At + "\",\"completedAt\":null}]";
        public const string State = "{\"gold\":80,\"gem\":3,\"exp\":20,\"energyStored\":15,\"energyUpdatedAt\":\"2026-10-18T20:21:22Z\","
            + "\"stageProgress\":[],\"items\":[],\"upgrades\":[],\"codex\":[],\"tutorials\":[\"intro_battle.dialogue\"],\"tutorialStates\":" + States + ",\"quests\":" + Boards + "}";
        public const string TutorialReceipt = "{\"tutorialId\":\"intro_battle\",\"dialogueOnly\":false,\"gold\":17,\"items\":{\"book.arrow\":2},"
            + "\"expBefore\":90,\"expAfter\":10,\"recordedAt\":\"" + At + "\"}";
        public const string QuestReceipt = "{\"id\":\"daily.login\",\"period\":\"DAILY\",\"periodKey\":\"20261019\",\"chest\":false,"
            + "\"points\":13,\"gold\":0,\"energy\":0,\"gems\":0,\"items\":{},\"claimedAt\":\"" + At + "\"}";
        private FakeHttpTransport _transport;
        private AuthSession _tokens;
        private HttpApiConfig _config;
        private HttpApiClient _client;

        [SetUp]
        public void SetUp()
        {
            _transport = new FakeHttpTransport { Response = new HttpResponseData(200, State) };
            _tokens = new AuthSession();
            _tokens.Set("account-a", "refresh-a");
            _config = new HttpApiConfig("https://example.test");
            _client = new HttpApiClient(_config, _transport, tokens: _tokens);
        }

        [Test]
        public void TutorialReceiptKeepsOriginalRewardAndExactSetLevelInsteadOfCurrentBalance()
        {
            _transport.Response = new HttpResponseData(200, TutorialReceipt);
            var receipt = new HttpTutorialApi(_client).Complete(new TutorialCompletionRequest("intro_battle")).GetAwaiter().GetResult();
            Assert.AreEqual(90, receipt.ExpBefore);
            Assert.AreEqual(10, receipt.ExpAfter, "서버 setLevel은 경험치를 낮출 수도 있다");
            Assert.AreEqual(17, receipt.Gold);
            Assert.AreEqual(2, receipt.Items["book.arrow"]);
            Assert.AreEqual("{\"tutorialId\":\"intro_battle\"}", _transport.Requests.Single().Body);
            Assert.That(_transport.Requests.Single().Url, Does.EndWith("/api/user/tutorials/complete"));
        }

        [Test]
        public void DialogueReceiptNeverCompletesOrGrantsAndGetRestoresOnlySeenState()
        {
            _transport.Queued.Enqueue(new HttpResponseData(200, "{\"tutorialId\":\"intro_battle.dialogue\",\"dialogueOnly\":true,\"gold\":0,\"items\":{},\"expBefore\":0,\"expAfter\":0,\"recordedAt\":\"" + At + "\"}"));
            _transport.Queued.Enqueue(new HttpResponseData(200, States));
            var api = new HttpTutorialApi(_client);
            Assert.IsTrue(api.Complete(new TutorialCompletionRequest("intro_battle.dialogue")).GetAwaiter().GetResult().DialogueOnly);
            var states = api.GetStates().GetAwaiter().GetResult();
            Assert.IsTrue(states.Single().dialogueSeen);
            Assert.IsFalse(states.Single().completed);
            Assert.IsFalse(states.Single().rewardClaimed);
        }

        [Test]
        public void ServerPeriodsAndClaimedPointsRestoreWithoutLocalClockOrBalanceMutation()
        {
            _transport.Response = new HttpResponseData(200, Boards);
            var boards = new HttpQuestApi(_client).GetBoards().GetAwaiter().GetResult();
            var state = JsonConvert.DeserializeObject<ServerUserData>(State,
                new JsonSerializerSettings { DateParseHandling = DateParseHandling.None });
            var snapshot = ServerUserMapper.ToSnapshot(state, "n", "public-id");
            Assert.AreEqual("20261019", boards[0].periodKey);
            Assert.AreEqual(13, snapshot.questPeriods[0].points);
            Assert.AreEqual(QuestPeriod.Weekly, snapshot.questPeriods[1].period);
            Assert.IsTrue(snapshot.quests.Single().claimed);
            CollectionAssert.AreEqual(new[] { "daily.chest" }, snapshot.questChests);
            CollectionAssert.AreEqual(new[] { "intro_battle.dialogue" }, snapshot.tutorials);
            Assert.IsFalse(snapshot.tutorialStates.Single().completed);
            var profile = new PlayerProfile(new GameDataStore());
            profile.Apply(snapshot);
            Assert.AreEqual(13, profile.QuestPoints(new QuestBoardDefinition
            {
                Id = "Daily",
                Quests = new List<QuestDefinition> { new() { Id = "daily.login", Points = 999 } },
            }), "서버 점수를 로컬 보상 정의로 다시 계산하지 않는다");
            Assert.IsTrue(profile.IsTutorialDone("intro_battle.dialogue"));
            Assert.IsFalse(profile.IsTutorialDone("intro_battle"));
            profile.Clear();
            Assert.IsEmpty(profile.Snapshot().questPeriods);
            Assert.IsEmpty(profile.Snapshot().tutorialStates);
        }

        [Test]
        public void LostQuestResponseReplaysSamePeriodAfterResetAndGetAloneDoesNotConfirmClaim()
        {
            var operation = QuestOperation();
            _transport.Queued.Enqueue(HttpResponseData.Failed(HttpFailure.Timeout));
            var first = operation.Submit().GetAwaiter().GetResult();
            Assert.AreEqual(ProgressSubmissionStatus.Unknown, first.Status);
            Assert.IsNull(first.Receipt);
            _transport.Queued.Enqueue(new HttpResponseData(200, QuestReceipt));
            var afterReset = JObject.Parse(State);
            afterReset["quests"][0]["periodKey"] = "20261020";
            _transport.Response = new HttpResponseData(200, afterReset.ToString());
            var restored = operation.Submit().GetAwaiter().GetResult();
            Assert.AreEqual(ProgressSubmissionStatus.Acknowledged, restored.Status);
            Assert.AreEqual("20261019", restored.Receipt.PeriodKey);
            Assert.AreEqual("20261020", restored.ObservedState.quests[0].periodKey);
            Assert.AreEqual(_transport.Requests[0].Body, _transport.Requests[2].Body);
            var request = JObject.Parse(_transport.Requests[0].Body);
            CollectionAssert.AreEquivalent(new[] { "id", "period", "periodKey" }, request.Properties().Select(p => p.Name));
            Assert.AreEqual("DAILY", request.Value<string>("period"));
        }

        [Test]
        public void ConfirmedTutorialThenGetFailureRetriesOnlyGetAndKeepsOriginalReceipt()
        {
            var operation = new HttpProgressReconciler(_config, _transport, _tokens).Tutorial(new TutorialCompletionRequest("intro_battle"));
            _transport.Queued.Enqueue(new HttpResponseData(200, TutorialReceipt));
            _transport.Queued.Enqueue(new HttpResponseData(500, ""));
            Assert.Throws<ApiException>(() => operation.Submit().GetAwaiter().GetResult());
            Assert.AreEqual(ProgressSubmissionStatus.Acknowledged, operation.Status);
            var result = operation.Submit().GetAwaiter().GetResult();
            Assert.AreEqual(17, result.Receipt.Gold);
            Assert.AreEqual(80, result.ObservedState.gold);
            CollectionAssert.AreEqual(new[] { HttpVerb.Post, HttpVerb.Get, HttpVerb.Get }, _transport.Requests.Select(r => r.Verb));
        }

        [TestCase(400)]
        [TestCase(409)]
        public void DefinitiveRejectionIsNeverReportedAsSuccessOrPostedAgain(int status)
        {
            _transport.Queued.Enqueue(new HttpResponseData(status, ""));
            var operation = QuestOperation();
            var rejected = operation.Submit().GetAwaiter().GetResult();
            Assert.AreEqual(ProgressSubmissionStatus.Rejected, rejected.Status);
            Assert.IsNull(rejected.Receipt);
            operation.Submit().GetAwaiter().GetResult();
            Assert.AreEqual(1, _transport.Requests.Count(r => r.Verb == HttpVerb.Post));
        }

        [TestCase(404)]
        [TestCase(503)]
        public void UnsupportedOrTransientEndpointNeverAppearsSuccessful(int status)
        {
            _transport.Queued.Enqueue(new HttpResponseData(status, ""));
            var result = QuestOperation().Submit().GetAwaiter().GetResult();
            Assert.AreEqual(ProgressSubmissionStatus.Unknown, result.Status);
            Assert.IsNull(result.Receipt);
        }

        [Test]
        public void TimeoutThenConflictKeepsUncertainPriorOutcome()
        {
            var operation = QuestOperation();
            _transport.Queued.Enqueue(HttpResponseData.Failed(HttpFailure.Timeout));
            operation.Submit().GetAwaiter().GetResult();
            _transport.Queued.Enqueue(new HttpResponseData(409, ""));
            Assert.AreEqual(ProgressSubmissionStatus.Unknown, operation.Submit().GetAwaiter().GetResult().Status);
        }

        [Test]
        public void NewLoginBlocksOldOperationBeforeAnyRequest()
        {
            var operation = QuestOperation();
            _tokens.Set("account-b", "refresh-b");
            Assert.AreEqual(HttpErrorMapper.SessionExpired, Assert.Throws<ApiException>(() => operation.Submit().GetAwaiter().GetResult()).Code);
            Assert.IsEmpty(_transport.Requests);
        }

        [Test]
        public void ConcurrentSubmissionAndLogoutDuringResponseCannotApplyOldAccount()
        {
            var transport = new PendingTransport();
            var operation = new HttpProgressReconciler(_config, transport, _tokens).Quest(Request());
            var pending = operation.Submit();
            Assert.Throws<InvalidOperationException>(() => operation.Submit().GetAwaiter().GetResult());
            _tokens.Set("account-b", "refresh-b");
            transport.Complete(QuestReceipt);
            Assert.AreEqual(HttpErrorMapper.SessionExpired, Assert.Throws<ApiException>(() => pending.GetAwaiter().GetResult()).Code);
            Assert.AreEqual(ProgressSubmissionStatus.Unknown, operation.Status);
            Assert.AreEqual(1, transport.Requests);
        }

        [Test]
        public void CancelBeforeDispatchDoesNotMarkRequestUnknownOrSendIt()
        {
            var operation = QuestOperation();
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            Assert.Throws<OperationCanceledException>(() => operation.Submit(cancellation.Token).GetAwaiter().GetResult());
            Assert.AreEqual(ProgressSubmissionStatus.NotSubmitted, operation.Status);
            Assert.IsEmpty(_transport.Requests);
        }

        [TestCase("id")]
        [TestCase("periodKey")]
        [TestCase("chest")]
        [TestCase("claimedAt")]
        [TestCase("items")]
        public void MissingReceiptFieldIsUncertainAndMustNotInventSuccess(string field)
        {
            var receipt = JObject.Parse(QuestReceipt);
            receipt.Property(field).Remove();
            _transport.Queued.Enqueue(new HttpResponseData(200, receipt.ToString()));
            var operation = QuestOperation();
            var result = operation.Submit().GetAwaiter().GetResult();
            Assert.AreEqual(ProgressSubmissionStatus.Unknown, result.Status);
            Assert.AreEqual(HttpErrorMapper.InvalidResponse, result.ErrorCode);
            Assert.IsNull(result.Receipt);
        }

        [TestCase("KILL_MONSTERS")]
        [TestCase("KillMonsters")]
        public void UnsupportedKillProgressIsNotMappedAsEnergyOrSuccess(string kind)
        {
            var boards = JArray.Parse(Boards);
            boards[0]["quests"][0]["kind"] = kind;
            _transport.Response = new HttpResponseData(200, boards.ToString());
            Assert.AreEqual(HttpErrorMapper.InvalidResponse, Assert.Throws<ApiException>(() => new HttpQuestApi(_client).GetBoards().GetAwaiter().GetResult()).Code);
        }

        [TestCase("duplicate")]
        [TestCase("points")]
        [TestCase("claimed")]
        [TestCase("date")]
        [TestCase("missing")]
        public void InvalidServerBoardCannotOverwriteRestoredProgress(string mutation)
        {
            var boards = JArray.Parse(Boards);
            switch (mutation)
            {
                case "duplicate": boards[1]["quests"] = boards[0]["quests"].DeepClone(); break;
                case "points": boards[0]["points"] = 0; break;
                case "claimed": boards[0]["quests"][0]["claimed"] = "true"; break;
                case "date": boards[0]["periodKey"] = "20260230"; break;
                case "missing": ((JObject)boards[0]).Property("chests").Remove(); break;
            }
            _transport.Response = new HttpResponseData(200, boards.ToString());
            Assert.Throws<ApiException>(() => new HttpQuestApi(_client).GetBoards().GetAwaiter().GetResult());
        }

        private HttpProgressOperation<QuestClaimReceipt> QuestOperation() => new HttpProgressReconciler(_config, _transport, _tokens).Quest(Request());
        private static QuestClaimRequest Request() => new("daily.login", QuestPeriod.Daily, "20261019");

        [Test]
        public void PostRefreshUnauthorizedRemainsRetryableAfterStateRefreshSucceeds()
        {
            _transport.Queued.Enqueue(new HttpResponseData(401, ""));
            _transport.Queued.Enqueue(new HttpResponseData(200, "{\"accessToken\":\"renewed-1\",\"refreshToken\":\"refresh-1\"}"));
            _transport.Queued.Enqueue(new HttpResponseData(401, ""));
            _transport.Queued.Enqueue(new HttpResponseData(401, ""));
            _transport.Queued.Enqueue(new HttpResponseData(200, "{\"accessToken\":\"renewed-2\",\"refreshToken\":\"refresh-2\"}"));
            _transport.Queued.Enqueue(new HttpResponseData(200, State));
            var operation = QuestOperation();
            var first = operation.Submit().GetAwaiter().GetResult();
            Assert.AreEqual(ProgressSubmissionStatus.Unknown, first.Status);
            Assert.AreEqual("HTTP_401", first.ErrorCode);
            Assert.IsNotNull(first.ObservedState);
            _transport.Queued.Enqueue(new HttpResponseData(200, QuestReceipt));
            Assert.AreEqual(ProgressSubmissionStatus.Acknowledged, operation.Submit().GetAwaiter().GetResult().Status);
            var posts = _transport.Requests.Where(r => r.Url.EndsWith("/quests/claim", StringComparison.Ordinal)).ToArray();
            Assert.AreEqual(3, posts.Length);
            Assert.That(posts.All(r => r.Body == posts[0].Body), Is.True);
        }

        [Test]
        public void ConcurrentClaimsInSameSessionShareOneRefreshAndKeepBothRequests()
        {
            var transport = new RefreshTransport();
            var session = new HttpProgressReconciler(_config, transport, _tokens);
            var first = session.Quest(Request()).Submit();
            var second = session.Quest(Request()).Submit();
            Assert.AreEqual(1, transport.RefreshRequests);
            transport.Refreshed();
            Assert.AreEqual(ProgressSubmissionStatus.Acknowledged, first.GetAwaiter().GetResult().Status);
            Assert.AreEqual(ProgressSubmissionStatus.Acknowledged, second.GetAwaiter().GetResult().Status);
            Assert.AreEqual(1, transport.RefreshRequests);
        }

        private sealed class RefreshTransport : IHttpTransport
        {
            private readonly UniTaskCompletionSource<HttpResponseData> _pending = new();
            public int RefreshRequests;
            public void Refreshed() => _pending.TrySetResult(new HttpResponseData(200,
                "{\"accessToken\":\"renewed\",\"refreshToken\":\"new-refresh\"}"));
            public UniTask<HttpResponseData> Send(HttpRequestData request, float timeoutSeconds, CancellationToken cancellationToken)
            {
                if (request.Url.EndsWith("/auth/refresh", StringComparison.Ordinal))
                { RefreshRequests++; return _pending.Task; }
                if (request.Headers["Authorization"] == "Bearer account-a")
                { return UniTask.FromResult(new HttpResponseData(401, "")); }
                return UniTask.FromResult(new HttpResponseData(200, request.Verb == HttpVerb.Post ? QuestReceipt : State));
            }
        }

        private sealed class PendingTransport : IHttpTransport
        {
            private readonly UniTaskCompletionSource<HttpResponseData> _pending = new();
            public int Requests;
            public void Complete(string body) => _pending.TrySetResult(new HttpResponseData(200, body));
            public UniTask<HttpResponseData> Send(HttpRequestData request, float timeoutSeconds, CancellationToken cancellationToken)
            { Requests++; return _pending.Task; }
        }
    }
}
