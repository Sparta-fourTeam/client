using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;
using Game.Network;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class ServerUpgradeTestBackendTests
    {
        private const string State = "{\"gold\":1000,\"gem\":3,\"exp\":2147483647,\"energyStored\":15,"
            + "\"energyUpdatedAt\":\"2026-10-16T23:00:00\",\"stageProgress\":[],\"items\":[],"
            + "\"upgrades\":[{\"upgradeId\":\"atk\",\"level\":0}],\"codex\":[],\"tutorials\":[]}";
        private const string Catalog = "[{\"upgradeId\":\"atk\",\"maxLevel\":3,\"unlockLevel\":1,\"costBase\":10,\"costStep\":0}]";
        private FakeHttpTransport _transport;
        private AuthSession _tokens;
        private ServerUpgradeTestBackend _backend;
        private GameDataStore _data;

        [SetUp]
        public void SetUp()
        {
            _transport = new FakeHttpTransport();
            _tokens = new AuthSession(); _tokens.Set("a", "r");
            _data = new GameDataStore();
            var definition = _data.Upgrades.GetOrThrow("atk");
            definition.MaxLevel = 3; definition.UnlockLevel = 1;
            definition.LevelCosts = new[] { 10, 10, 10 }; definition.MaterialItemId = null;
            _backend = new ServerUpgradeTestBackend(new HttpApiConfig("https://example.test"), _transport, _tokens, _data);
            _transport.Queued.Enqueue(new HttpResponseData(200, State));
            _backend.GetMe().GetAwaiter().GetResult();
        }

        private static string UpdatedState()
        {
            var state = JObject.Parse(State); state["gold"] = 990; state["upgrades"][0]["level"] = 1;
            return state.ToString();
        }

        private static string ClaimState(int clear, int claimed, int gold = 1000)
        {
            var state = JObject.Parse(State); state["gold"] = gold;
            state["stageProgress"] = JArray.Parse($"[{{\"stageId\":1,\"clearRating\":{clear},\"claimedRating\":{claimed}}},{{\"stageId\":2,\"clearRating\":1,\"claimedRating\":0}}]");
            return state.ToString();
        }

        private void LoadClaimState()
        {
            _transport.Queued.Enqueue(new HttpResponseData(200, ClaimState(2, 0)));
            _backend.GetMe().GetAwaiter().GetResult();
        }

        [TestCase(200)]
        [TestCase(409)]
        [TestCase(500)]
        [TestCase(0)]
        [Test(Description = "별 보상 POST 확인/409/5xx/유실 뒤에는 최초 목표를 보존하고 GET만 하며 강화·다른 수령을 막는다")]
        public void StageClaim_Unconfirmed_OnlyReadsFrozenTarget(int status)
        {
            LoadClaimState();
            _transport.Queued.Enqueue(status == 0 ? HttpResponseData.Failed(HttpFailure.Timeout)
                : new HttpResponseData(status, "{\"stageId\":1,\"clearRating\":2,\"claimedRating\":2}"));
            _transport.Queued.Enqueue(new HttpResponseData(200, ClaimState(3, 0)));
            var pending = Assert.Throws<ApiException>(() => _backend.ClaimRatingReward(1).GetAwaiter().GetResult());
            Assert.AreEqual("STAGE_REWARD_PENDING", pending.Code);
            Assert.AreEqual(2, _backend.PendingTargetRating);
            Assert.Throws<ApiException>(() => _backend.Purchase("atk", "new-key").GetAwaiter().GetResult());
            Assert.Throws<ApiException>(() => _backend.ClaimRatingReward(2).GetAwaiter().GetResult());
            _transport.Queued.Enqueue(new HttpResponseData(500, ""));
            Assert.Throws<ApiException>(() => _backend.RecoverStageReward().GetAwaiter().GetResult());
            _transport.Queued.Enqueue(new HttpResponseData(200, ClaimState(3, 2, 700)));
            var state = _backend.RecoverStageReward().GetAwaiter().GetResult();
            Assert.AreEqual(700, state.gold, "잔액 증가를 성공 조건으로 사용하지 않는다");
            Assert.AreEqual(2, state.stageProgress.Single(row => row.stageId == 1).claimedRating);
            Assert.IsFalse(_backend.HasPendingStageReward);
            var post = _transport.Requests.Single(row => row.Verb == HttpVerb.Post);
            Assert.AreEqual("https://example.test/api/user/stage-progress/claim", post.Url);
            Assert.AreEqual(1, (int)JObject.Parse(post.Body)["stageId"]);
            Assert.AreEqual(1, JObject.Parse(post.Body).Count);
        }

        [Test(Description = "ACK가 더 높은 등급을 확인했으면 그보다 낮은 GET 상태를 적용하지 않는다")]
        public void StageClaim_AcknowledgedHigherRating_WaitsForMatchingState()
        {
            LoadClaimState();
            _transport.Queued.Enqueue(new HttpResponseData(200, "{\"stageId\":1,\"clearRating\":3,\"claimedRating\":3}"));
            _transport.Queued.Enqueue(new HttpResponseData(200, ClaimState(3, 2)));
            Assert.Throws<ApiException>(() => _backend.ClaimRatingReward(1).GetAwaiter().GetResult());
            Assert.AreEqual(2, _backend.PendingTargetRating);
            _transport.Queued.Enqueue(new HttpResponseData(200, ClaimState(3, 3)));
            _backend.RecoverStageReward().GetAwaiter().GetResult();
            Assert.AreEqual(1, _transport.Requests.Count(row => row.Verb == HttpVerb.Post));
        }

        [Test(Description = "부분 GET 응답으로 수령을 확정하거나 잠금을 해제하지 않는다")]
        public void StageClaim_PartialState_RetainsReadOnlyRecovery()
        {
            LoadClaimState();
            _transport.Queued.Enqueue(new HttpResponseData(200, "{\"stageId\":1,\"clearRating\":2,\"claimedRating\":2}"));
            _transport.Queued.Enqueue(new HttpResponseData(200, "{}"));
            Assert.Throws<ApiException>(() => _backend.ClaimRatingReward(1).GetAwaiter().GetResult());
            Assert.IsTrue(_backend.HasPendingStageReward);
            _transport.Queued.Enqueue(new HttpResponseData(200, ClaimState(2, 2)));
            _backend.RecoverStageReward().GetAwaiter().GetResult();
            Assert.AreEqual(1, _transport.Requests.Count(row => row.Verb == HttpVerb.Post));
        }

        [Test(Description = "미확정 강화가 있으면 별 보상 POST를 보내지 않는다")]
        public void PendingUpgrade_BlocksStageClaim()
        {
            LoadClaimState(); QuoteAndResponse(HttpResponseData.Failed(HttpFailure.Timeout));
            Assert.Throws<ApiException>(() => _backend.Purchase("atk", "key").GetAwaiter().GetResult());
            var error = Assert.Throws<ApiException>(() => _backend.ClaimRatingReward(1).GetAwaiter().GetResult());
            Assert.AreEqual("UPGRADE_RECOVERY_REQUIRED", error.Code);
            Assert.IsFalse(_transport.Requests.Any(row => row.Url.EndsWith("/stage-progress/claim")));
        }

        [Test(Description = "이미 수령한 등급은 POST 없이 거절한다")]
        public void StageClaim_AlreadyClaimed_DoesNotPost()
        {
            _transport.Queued.Enqueue(new HttpResponseData(200, ClaimState(2, 2)));
            _backend.GetMe().GetAwaiter().GetResult();
            var error = Assert.Throws<ApiException>(() => _backend.ClaimRatingReward(1).GetAwaiter().GetResult());
            Assert.AreEqual("NO_REWARD", error.Code);
            Assert.IsFalse(_transport.Requests.Any(row => row.Verb == HttpVerb.Post));
        }

        [Test(Description = "409 자체가 아니라 후속 수령 상태로만 확인한다")]
        public void StageClaim_ConflictThenCoveredTarget_ReturnsObservedState()
        {
            LoadClaimState();
            _transport.Queued.Enqueue(new HttpResponseData(409, ""));
            _transport.Queued.Enqueue(new HttpResponseData(200, ClaimState(2, 2, 600)));
            var snapshot = _backend.ClaimRatingReward(1).GetAwaiter().GetResult();
            Assert.AreEqual(2, snapshot.stageProgress.Single(row => row.stageId == 1).claimedRating);
            Assert.IsFalse(_backend.HasPendingStageReward);
            Assert.AreEqual(1, _transport.Requests.Count(row => row.Verb == HttpVerb.Post));
        }

        [TestCase(false)]
        [TestCase(true)]
        [Test(Description = "별 보상 POST/GET 도중 계정 전환이면 이전 응답을 새 계정에 반환하지 않는다")]
        public void StageClaim_AccountChange_DiscardsResponse(bool duringRead)
        {
            var transport = new ClaimSwitchingTransport(_tokens, duringRead);
            var backend = new ServerUpgradeTestBackend(new HttpApiConfig("https://example.test"), transport, _tokens, _data);
            backend.GetMe().GetAwaiter().GetResult();
            var error = Assert.Throws<ApiException>(() => backend.ClaimRatingReward(1).GetAwaiter().GetResult());
            Assert.AreEqual(HttpErrorMapper.SessionExpired, error.Code);
            Assert.IsFalse(backend.HasPendingStageReward);
            Assert.Throws<InvalidOperationException>(() => backend.RecoverStageReward().GetAwaiter().GetResult());
            Assert.AreEqual(1, transport.Posts);
        }

        private sealed class ClaimSwitchingTransport : IHttpTransport
        {
            private readonly AuthSession _tokens;
            private readonly bool _duringRead;
            public int Posts;
            public ClaimSwitchingTransport(AuthSession tokens, bool duringRead) { _tokens = tokens; _duringRead = duringRead; }
            public UniTask<HttpResponseData> Send(HttpRequestData request, float timeoutSeconds, CancellationToken token)
            {
                if (request.Verb == HttpVerb.Post)
                {
                    Posts++; if (!_duringRead) { _tokens.Set("b", "r-b"); }
                    return UniTask.FromResult(new HttpResponseData(200, "{\"stageId\":1,\"clearRating\":2,\"claimedRating\":2}"));
                }
                if (Posts > 0 && _duringRead) { _tokens.Set("b", "r-b"); }
                return UniTask.FromResult(new HttpResponseData(200, ClaimState(2, Posts > 0 ? 2 : 0)));
            }
        }

        private void QuoteAndResponse(HttpResponseData response)
        {
            _transport.Queued.Enqueue(new HttpResponseData(200, Catalog));
            _transport.Queued.Enqueue(response);
        }

        [Test(Description = "서버 단건 구매와 후속 상태 조회가 확인된 뒤에만 최신 지갑과 레벨을 반환한다")]
        public void Purchase_ReturnsServerState_AfterValidQuoteAndAcknowledgement()
        {
            QuoteAndResponse(new HttpResponseData(200, "{\"upgradeId\":\"atk\",\"level\":1}"));
            _transport.Queued.Enqueue(new HttpResponseData(200, UpdatedState()));
            var state = _backend.Purchase("atk", "key-one").GetAwaiter().GetResult();
            Assert.AreEqual(990, state.gold);
            Assert.AreEqual(1, state.upgrades.Single().level);
            Assert.IsFalse(_backend.HasPendingPurchase);
            var body = JObject.Parse(_transport.Requests.Single(r => r.Verb == HttpVerb.Post).Body);
            Assert.AreEqual("key-one", (string)body["createId"]);
        }

        [TestCase(409)]
        [TestCase(408)]
        [TestCase(429)]
        [TestCase(500)]
        [TestCase(0)]
        [TestCase(200)]
        [Test(Description = "중복 처리중/서버 오류/응답 유실 뒤 새 구매를 막고 같은 createId로만 복구한다")]
        public void UnknownPurchase_ReusesExactKey_AndBlocksNewPurchase(int status)
        {
            QuoteAndResponse(status == 0 ? HttpResponseData.Failed(HttpFailure.Timeout) : new HttpResponseData(status, status == 200 ? "{}" : ""));
            Assert.Throws<ApiException>(() => _backend.Purchase("atk", "original-key").GetAwaiter().GetResult());
            Assert.IsTrue(_backend.HasPendingPurchase);
            var error = Assert.Throws<ApiException>(() => _backend.Purchase("atk", "new-key").GetAwaiter().GetResult());
            Assert.AreEqual("UPGRADE_RECOVERY_REQUIRED", error.Code);
            _transport.Queued.Enqueue(new HttpResponseData(200, "{\"upgradeId\":\"atk\",\"level\":1}"));
            _transport.Queued.Enqueue(new HttpResponseData(200, UpdatedState()));
            _backend.RecoverPurchase().GetAwaiter().GetResult();
            var posts = _transport.Requests.Where(r => r.Verb == HttpVerb.Post).ToArray();
            Assert.AreEqual(2, posts.Length);
            Assert.AreEqual(posts[0].Body, posts[1].Body);
        }

        [TestCase(403)]
        [TestCase(422)]
        [Test(Description = "최초 구매가 확정 거절되면 미확정 잠금을 남기지 않는다")]
        public void DefinitiveRejection_ReleasesPending(int status)
        {
            QuoteAndResponse(new HttpResponseData(status, ""));
            Assert.Throws<ApiException>(() => _backend.Purchase("atk", "key").GetAwaiter().GetResult());
            Assert.IsFalse(_backend.HasPendingPurchase);
        }

        [Test(Description = "토큰 갱신 후에도 구매가 401 거절되면 새 구매를 영구 잠그지 않는다")]
        public void SecondUnauthorized_AfterRefresh_ReleasesPending()
        {
            QuoteAndResponse(new HttpResponseData(401, ""));
            _transport.Queued.Enqueue(new HttpResponseData(200, "{\"accessToken\":\"renewed\",\"refreshToken\":\"renewed-r\"}"));
            _transport.Queued.Enqueue(new HttpResponseData(401, ""));
            var error = Assert.Throws<ApiException>(() => _backend.Purchase("atk", "key").GetAwaiter().GetResult());
            Assert.AreEqual("HTTP_401", error.Code);
            Assert.IsFalse(_backend.HasPendingPurchase);
        }

        [Test(Description = "이전 응답 유실 뒤 받은 403은 이전 지급의 취소 증거가 아니므로 기존 키를 잊지 않는다")]
        public void DefinitiveRejection_AfterLostResponse_PreservesEarlierUncertainty()
        {
            QuoteAndResponse(HttpResponseData.Failed(HttpFailure.Timeout));
            Assert.Throws<ApiException>(() => _backend.Purchase("atk", "original-key").GetAwaiter().GetResult());
            _transport.Queued.Enqueue(new HttpResponseData(403, ""));
            Assert.Throws<ApiException>(() => _backend.RecoverPurchase().GetAwaiter().GetResult());
            Assert.IsTrue(_backend.HasPendingPurchase);
            var error = Assert.Throws<ApiException>(() => _backend.Purchase("atk", "new-key").GetAwaiter().GetResult());
            Assert.AreEqual("UPGRADE_RECOVERY_REQUIRED", error.Code);
        }

        [TestCase("{}")]
        [TestCase("failed")]
        [TestCase("stale")]
        [Test(Description = "구매 확인 뒤 부분/실패/이전 레벨 조회를 완료로 오판하지 않고 GET만 재시도한다")]
        public void AcknowledgedPurchase_SnapshotFailure_RecoveryOnlyGets(string response)
        {
            QuoteAndResponse(new HttpResponseData(200, "{\"upgradeId\":\"atk\",\"level\":1}"));
            _transport.Queued.Enqueue(response == "failed" ? new HttpResponseData(500, "")
                : new HttpResponseData(200, response == "stale" ? State : response));
            Assert.Throws<ApiException>(() => _backend.Purchase("atk", "key").GetAwaiter().GetResult());
            _transport.Queued.Enqueue(new HttpResponseData(200, UpdatedState()));
            _backend.RecoverPurchase().GetAwaiter().GetResult();
            Assert.AreEqual(1, _transport.Requests.Count(r => r.Verb == HttpVerb.Post));
            Assert.AreEqual(HttpVerb.Get, _transport.Requests.Last().Verb);
        }

        [Test(Description = "서버 가격과 표시 가격이 다르면 돈을 쓰는 요청을 보내지 않는다")]
        public void CatalogMismatch_DoesNotPurchase()
        {
            _transport.Queued.Enqueue(new HttpResponseData(200, Catalog.Replace("\"costBase\":10", "\"costBase\":99")));
            var error = Assert.Throws<ApiException>(() => _backend.Purchase("atk", "key").GetAwaiter().GetResult());
            Assert.AreEqual("UPGRADE_CATALOG_MISMATCH", error.Code);
            Assert.IsFalse(_backend.HasPendingPurchase);
            Assert.IsFalse(_transport.Requests.Any(r => r.Verb == HttpVerb.Post));
        }

        [Test(Description = "인증 재발급은 로그인 세대를 바꾸지 않으며 강화 재전송에도 같은 본문과 키를 유지한다")]
        public void TokenRefresh_ReplaysIdenticalPurchase()
        {
            QuoteAndResponse(new HttpResponseData(401, ""));
            _transport.Queued.Enqueue(new HttpResponseData(200, "{\"accessToken\":\"renewed\",\"refreshToken\":\"renewed-r\"}"));
            _transport.Queued.Enqueue(new HttpResponseData(200, "{\"upgradeId\":\"atk\",\"level\":1}"));
            _transport.Queued.Enqueue(new HttpResponseData(200, UpdatedState()));

            _backend.Purchase("atk", "original-key").GetAwaiter().GetResult();

            var posts = _transport.Requests.Where(r => r.Url.EndsWith("/user/upgrades")).ToArray();
            Assert.AreEqual(2, posts.Length);
            Assert.AreEqual(posts[0].Body, posts[1].Body);
            Assert.AreEqual("Bearer renewed", posts[1].Headers["Authorization"]);
            Assert.IsFalse(_backend.HasPendingPurchase);
        }

        [Test(Description = "계정 전환 전 구매 응답은 반환하지 않으며 새 계정으로 이전 구매를 복구하지 않는다")]
        public void AccountChangeDuringPurchase_DiscardsResponse()
        {
            var transport = new SwitchingTransport(_tokens);
            var backend = new ServerUpgradeTestBackend(new HttpApiConfig("https://example.test"), transport, _tokens, _data);
            backend.GetMe().GetAwaiter().GetResult();
            var error = Assert.Throws<ApiException>(() => backend.Purchase("atk", "key").GetAwaiter().GetResult());
            Assert.AreEqual(HttpErrorMapper.SessionExpired, error.Code);
            Assert.IsFalse(backend.HasPendingPurchase);
            Assert.Throws<InvalidOperationException>(() => backend.RecoverPurchase().GetAwaiter().GetResult());
            Assert.AreEqual(1, transport.Posts);
        }

        private sealed class SwitchingTransport : IHttpTransport
        {
            private readonly AuthSession _session;
            public int Posts;
            public SwitchingTransport(AuthSession session) => _session = session;
            public UniTask<HttpResponseData> Send(HttpRequestData request, float timeoutSeconds, CancellationToken token)
            {
                if (request.Verb == HttpVerb.Post)
                {
                    Posts++; _session.Set("b", "rb");
                    return UniTask.FromResult(new HttpResponseData(200, "{\"upgradeId\":\"atk\",\"level\":1}"));
                }
                return UniTask.FromResult(new HttpResponseData(200, request.Url.EndsWith("/upgrade") ? Catalog : State));
            }
        }
    }
}
