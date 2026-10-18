using System;
using System.Collections;
using System.Collections.Generic;
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
    public sealed class HttpBattleProgressLiveTests
    {
        [UnityTest]
        public IEnumerator CumulativeReportsAndLostAbandonAckReachIsolatedServerExactlyOnce() => UniTask.ToCoroutine(async () =>
        {
            var path = Environment.GetEnvironmentVariable("CHECKPOINT_HTTP_FIXTURE");
            if (string.IsNullOrEmpty(path)) { Assert.Ignore("격리 checkpoint HTTP fixture가 필요합니다"); }
            var fixture = JObject.Parse(File.ReadAllText(path));
            var url = new Uri(fixture.Value<string>("baseUrl"));
            Assert.AreEqual("127.0.0.1", url.Host);
            var config = new HttpApiConfig(url.ToString(), 10);
            var auth = new AuthSession();
            auth.Set(fixture.Value<string>("tokenA"), null);
            var transport = new LoseProgressResponse();
            var client = new HttpApiClient(config, transport, tokens: auth);
            var api = new HttpBattleProgressApi(client, 901);
            var request = new BattleProgressRequest
            {
                battleId = fixture.Value<string>("battleId"),
                reportId = Guid.NewGuid().ToString("D"),
                expectedRevision = 0,
                killedMonsters = 3,
                completedWaves = 1,
                reachedWave = 1,
                encounteredMonsterIds = new List<int> { 1, 2 },
            };
            transport.LoseNext = true;
            try { await api.ReportProgress(request); Assert.Fail("성공 응답 유실을 재현해야 합니다"); }
            catch (ApiException error) { Assert.AreEqual(ApiErrorKind.Transient, error.Kind); }
            var replay = await new HttpBattleProgressApi(client, 901).ReportProgress(request);
            Assert.AreEqual(1, replay.revision);
            Assert.AreEqual(3, replay.acceptedDelta);
            request = new BattleProgressRequest
            {
                battleId = request.battleId,
                reportId = Guid.NewGuid().ToString("D"),
                expectedRevision = 1,
                killedMonsters = 7,
                completedWaves = 1,
                reachedWave = 2,
                encounteredMonsterIds = new List<int> { 1, 2 },
            };
            transport.LoseNext = true;
            try { await api.AbandonProgress(request); Assert.Fail("닫힌 전투의 응답 유실을 재현해야 합니다"); }
            catch (ApiException error) { Assert.AreEqual(ApiErrorKind.Transient, error.Kind); }
            replay = await new HttpBattleProgressApi(client, 901).AbandonProgress(request);
            Assert.AreEqual(2, replay.revision);
            Assert.AreEqual(4, replay.acceptedDelta);
            var state = await client.Get<JObject>("api/battle/progress/" + request.battleId);
            Assert.IsTrue(state.Value<bool>("closed"));
            Assert.AreEqual(7, state["progress"].Value<int>("killedMonsters"));
            var board = (await new HttpQuestApi(client).GetBoards()).Single(b => b.period == "DAILY");
            Assert.AreEqual(7, board.quests.Single(q => q.id == "daily.fixture.kill").count);
            var me = await client.Get<JObject>("api/user/me");
            Assert.AreEqual(0, me.Value<int>("gold"), "중간 집계와 포기는 최종 전투 보상을 지급하지 않는다");
            Assert.AreEqual(4, transport.AcceptedPosts);
        });

        private sealed class LoseProgressResponse : IHttpTransport
        {
            private readonly UnityWebRequestTransport _inner = new();
            public bool LoseNext;
            public int AcceptedPosts;
            public async UniTask<HttpResponseData> Send(HttpRequestData request, float timeoutSeconds, CancellationToken cancellationToken)
            {
                var response = await _inner.Send(request, timeoutSeconds, cancellationToken);
                if (request.Verb == HttpVerb.Post && !request.Url.EndsWith("/open", StringComparison.Ordinal) && response.StatusCode == 200)
                {
                    AcceptedPosts++;
                    if (LoseNext) { LoseNext = false; return HttpResponseData.Failed(HttpFailure.Timeout); }
                }
                return response;
            }
        }
    }
}
