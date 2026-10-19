using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;
using Game.Network;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Game.Tests
{
    /// <summary>명시한 격리 서버에서만 실행하는 실제 HTTP 검증. 운영 주소나 기본 로컬 서버로 대체하지 않는다.</summary>
    public sealed class HttpAccountProgressLiveTests
    {
        [UnityTest]
        public IEnumerator IsolatedServerRestoresTutorialQuestChestAndReplaysLostResponse() => UniTask.ToCoroutine(async () =>
        {
            var fixturePath = Environment.GetEnvironmentVariable("QUEST61_HTTP_FIXTURE");
            if (string.IsNullOrEmpty(fixturePath)) { Assert.Ignore("격리 HTTP fixture가 지정되지 않았습니다"); }
            var fixture = JObject.Parse(File.ReadAllText(fixturePath));
            var endpoint = new Uri(fixture.Value<string>("baseUrl"));
            Assert.AreEqual("127.0.0.1", endpoint.Host, "실제 테스트는 독립 루프백 서버만 허용한다");
            var config = new HttpApiConfig(endpoint.ToString(), 10);
            var tokens = new AuthSession();
            tokens.Set(fixture.Value<string>("tokenA"), null);
            var transport = new LostResponseTransport();
            var client = new HttpApiClient(config, transport, tokens: tokens);
            var tutorials = new HttpTutorialApi(client);
            var reconciler = new HttpProgressReconciler(config, transport, tokens);
            var dialogue = await tutorials.Complete(new TutorialCompletionRequest("intro_fixture.dialogue"));
            Assert.IsTrue(dialogue.DialogueOnly);
            Assert.AreEqual(0, dialogue.Gold);
            var states = await tutorials.GetStates();
            Assert.IsFalse(states.Single().completed);

            var completion = reconciler.Tutorial(new TutorialCompletionRequest("intro_fixture"));
            var completed = await completion.Submit();
            Assert.AreEqual(ProgressSubmissionStatus.Acknowledged, completed.Status);
            Assert.AreEqual(17, completed.Receipt.Gold);
            Assert.IsTrue(completed.ObservedState.tutorialStates.Single().completed);
            Assert.IsTrue(completed.ObservedState.tutorialStates.Single().rewardClaimed);

            var quests = new HttpQuestApi(client);
            var board = (await quests.GetBoards()).Single(b => b.period == "DAILY");
            var request = new QuestClaimRequest("daily.fixture", QuestPeriod.Daily, board.periodKey);
            var claimed = await reconciler.Quest(request).Submit();
            Assert.AreEqual(13, claimed.Receipt.Points);
            Assert.AreEqual(17, claimed.ObservedState.gold, "퀘스트 점수 수령은 골드를 주지 않는다");

            var chestRequest = new QuestClaimRequest("daily.chest", QuestPeriod.Daily, board.periodKey);
            var chest = reconciler.Quest(chestRequest, chest: true);
            transport.LoseNextPost = true;
            var uncertain = await chest.Submit();
            Assert.AreEqual(ProgressSubmissionStatus.Unknown, uncertain.Status);
            Assert.IsNull(uncertain.Receipt);
            Assert.AreEqual(34, uncertain.ObservedState.gold, "현재 상태가 바뀌어도 유실된 영수증을 대신하지 않는다");
            var restored = await chest.Submit();
            Assert.AreEqual(ProgressSubmissionStatus.Acknowledged, restored.Status);
            Assert.AreEqual(17, restored.Receipt.Gold);
            Assert.AreEqual(34, restored.ObservedState.gold);
            Assert.AreEqual(7, restored.ObservedState.gem);
            Assert.AreEqual(4, restored.ObservedState.items.Single(i => i.itemId.Value<string>() == "book.fixture").quantity);
            Assert.AreEqual(2, transport.ChestPosts);

            var path = Path.Combine(Path.GetTempPath(), $"quest61-live-{Guid.NewGuid():N}.json");
            try
            {
                var local = new LocalSaveStore(path);
                local.Flush(local.Load());
                var before = File.ReadAllBytes(path);
                var otherDevice = new AuthSession();
                otherDevice.Set(fixture.Value<string>("tokenA"), null);
                var player = new HttpPlayerApi(new HttpApiClient(config, new UnityWebRequestTransport(), tokens: otherDevice),
                    new LocalPlayerApi(local), otherDevice);
                var snapshot = await player.GetMe();
                Assert.AreEqual(34, snapshot.gold);
                Assert.Contains("intro_fixture", snapshot.tutorials);
                Assert.Contains("intro_fixture.dialogue", snapshot.tutorials);
                Assert.IsTrue(snapshot.quests.Single(q => q.questId == "daily.fixture").claimed);
                Assert.Contains("daily.chest", snapshot.questChests);
                CollectionAssert.AreEqual(before, File.ReadAllBytes(path));
                otherDevice.Set(fixture.Value<string>("tokenB"), null);
                var other = await player.GetMe();
                Assert.IsEmpty(other.tutorials);
                Assert.IsEmpty(other.questChests);
                Assert.AreEqual(0, other.gold);
            }
            finally { if (File.Exists(path)) { File.Delete(path); } }
        });

        private sealed class LostResponseTransport : IHttpTransport
        {
            private readonly UnityWebRequestTransport _inner = new();
            public bool LoseNextPost;
            public int ChestPosts;
            public async UniTask<HttpResponseData> Send(HttpRequestData request, float timeoutSeconds, CancellationToken cancellationToken)
            {
                if (request.Url.EndsWith("/quests/chests/claim", StringComparison.Ordinal)) { ChestPosts++; }
                var response = await _inner.Send(request, timeoutSeconds, cancellationToken);
                if (LoseNextPost && request.Verb == HttpVerb.Post && response.StatusCode == 200)
                {
                    LoseNextPost = false;
                    return HttpResponseData.Failed(HttpFailure.Timeout);
                }
                return response;
            }
        }
    }
}
