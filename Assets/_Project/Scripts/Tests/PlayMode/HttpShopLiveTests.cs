using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Network;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Game.Tests
{
    /// <summary>명시된 격리 루프백 서버에서만 실제 UnityWebRequest로 상점 HTTP 계약을 확인한다.</summary>
    public sealed class HttpShopLiveTests
    {
        [UnityTest]
        public IEnumerator RestoresAnotherSessionAndReplaysTheSamePurchaseAfterLostResponse() => UniTask.ToCoroutine(async () =>
        {
            string path = Environment.GetEnvironmentVariable("SHOP526_HTTP_FIXTURE");
            if (string.IsNullOrEmpty(path)) { Assert.Ignore("격리 상점 HTTP fixture가 지정되지 않았습니다"); }
            var fixture = JObject.Parse(File.ReadAllText(path));
            var endpoint = new Uri(fixture.Value<string>("baseUrl"));
            Assert.AreEqual("127.0.0.1", endpoint.Host, "격리 루프백 서버만 허용한다");

            var config = new HttpApiConfig(endpoint.ToString(), 10);
            var firstSession = new AuthSession();
            firstSession.Set(fixture.Value<string>("tokenA"), null);
            var transport = new LostResponseTransport();
            var firstApi = new HttpShopApi(config, transport, firstSession);
            var initial = await firstApi.Restore();
            Assert.AreEqual(fixture.Value<int>("initialGold"), initial.Account.Gold);
            Assert.AreEqual(fixture.Value<int>("initialGems"), initial.Account.Gems);
            Assert.AreEqual(fixture.Value<int>("offerPrice"), initial.Account.Daily.Offers[0].Price);
            Assert.AreEqual(fixture.Value<int>("packPrice"), initial.GoldPacks.Packs[0].GemPrice);

            string requestId = Guid.NewGuid().ToString("N");
            var exchange = new HttpShopExchange(firstApi,
                ShopExchangeRequest.Buy(initial.Account.Daily, 0, requestId));
            transport.LoseNextPost = true;
            var unknown = await exchange.Submit();
            Assert.AreEqual(ShopSubmissionStatus.Unknown, unknown.Status);
            Assert.IsNull(unknown.DailyReceipt);
            Assert.AreEqual(initial.Account.Gems - initial.Account.Daily.Offers[0].Price, unknown.Account.Gems);

            var replayed = await exchange.Submit();
            Assert.AreEqual(ShopSubmissionStatus.Acknowledged, replayed.Status);
            Assert.AreEqual(requestId, replayed.DailyReceipt.RequestId);
            Assert.AreEqual(initial.Account.Daily.Offers[0].Price, replayed.DailyReceipt.GemsSpent);
            Assert.AreEqual(2, transport.BuyBodies.Count);
            Assert.AreEqual(transport.BuyBodies[0], transport.BuyBodies[1]);
            CollectionAssert.AreEquivalent(new[] { "requestId", "dayKey", "rerollIndex", "slot" },
                JObject.Parse(transport.BuyBodies[0]).Properties().Select(p => p.Name));

            var secondSession = new AuthSession();
            secondSession.Set(fixture.Value<string>("tokenB"), null);
            var otherApi = new HttpShopApi(config, new UnityWebRequestTransport(), secondSession);
            var restored = await otherApi.Restore();
            Assert.AreEqual(unknown.Account.Gems, restored.Account.Gems);
            CollectionAssert.Contains(restored.Account.Daily.Purchased, 0);
            Assert.AreEqual(initial.Account.Daily.Offers[0].OfferId, restored.Account.Daily.Offers[0].OfferId);

            var pack = new HttpShopExchange(otherApi,
                ShopExchangeRequest.GoldPack(restored.GoldPacks, 0, Guid.NewGuid().ToString("N")));
            var gold = await pack.Submit();
            Assert.AreEqual(ShopSubmissionStatus.Acknowledged, gold.Status);
            Assert.AreEqual(fixture.Value<int>("packGold"), gold.GoldPackReceipt.Gold);
            Assert.AreEqual(fixture.Value<int>("packPrice"), gold.GoldPackReceipt.GemsSpent);
            Assert.AreEqual(initial.Account.Gold + gold.GoldPackReceipt.Gold, gold.Account.Gold);
            Assert.AreEqual(restored.Account.Gems - gold.GoldPackReceipt.GemsSpent, gold.Account.Gems);
        });

        private sealed class LostResponseTransport : IHttpTransport
        {
            private readonly UnityWebRequestTransport _inner = new();
            public readonly List<string> BuyBodies = new();
            public bool LoseNextPost;

            public async UniTask<HttpResponseData> Send(HttpRequestData request, float timeoutSeconds,
                CancellationToken cancellationToken)
            {
                bool buy = request.Verb == HttpVerb.Post
                    && request.Url.EndsWith("/api/user/shop/buy", StringComparison.Ordinal);
                if (buy) { BuyBodies.Add(request.Body); }
                var response = await _inner.Send(request, timeoutSeconds, cancellationToken);
                if (buy && LoseNextPost && response.StatusCode == 200)
                {
                    LoseNextPost = false;
                    return HttpResponseData.Failed(HttpFailure.Timeout);
                }
                return response;
            }
        }
    }
}
