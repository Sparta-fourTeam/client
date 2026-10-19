using System;
using System.IO;
using System.Linq;
using System.Text;
using Cysharp.Threading.Tasks;
using Game.Core;
using Game.Network;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class HttpShopExchangeTests
    {
        private FakeHttpTransport _transport;
        private AuthSession _tokens;
        private HttpShopApi _api;

        [SetUp]
        public void SetUp()
        {
            _transport = new FakeHttpTransport();
            _tokens = new AuthSession();
            _tokens.Set("account-a", "refresh-a");
            _api = new HttpShopApi(new HttpApiConfig("https://example.test"), _transport, _tokens);
        }

        [Test(Description = "새 인증 세션은 user/me의 실제 지갑·구매 칸과 서버 카탈로그 가격을 복원한다")]
        public void Restore_UsesServerStockAndWallet_AcrossSessions()
        {
            _transport.Queued.Enqueue(Ok(Account(40, 12, Daily(purchased: false))));
            _transport.Queued.Enqueue(Ok(Packs()));
            var first = _api.Restore().GetAwaiter().GetResult();
            Assert.AreEqual(40, first.Account.Gold);
            Assert.AreEqual(12, first.Account.Gems);
            Assert.AreEqual("book.arrow", first.Account.Daily.Offers[0].Items.Keys.Single());
            Assert.AreEqual(7, first.Account.Daily.Offers[0].Price);
            Assert.AreEqual(9, first.GoldPacks.Packs[0].GemPrice);

            var otherDevice = new AuthSession();
            otherDevice.Set("account-a-on-device-b", "refresh-b");
            var restored = new HttpShopApi(new HttpApiConfig("https://example.test"), _transport, otherDevice);
            _transport.Queued.Enqueue(Ok(Account(40, 5, Daily(purchased: true))));
            _transport.Queued.Enqueue(Ok(Packs()));
            var second = restored.Restore().GetAwaiter().GetResult();
            Assert.AreEqual(5, second.Account.Gems);
            CollectionAssert.AreEqual(new[] { 0 }, second.Account.Daily.Purchased);
            Assert.AreEqual("Bearer account-a", _transport.Requests[0].Headers["Authorization"]);
            Assert.AreEqual("Bearer account-a-on-device-b", _transport.Requests[2].Headers["Authorization"]);
            Assert.That(_transport.Requests.All(r => r.Verb == HttpVerb.Get));
        }

        [Test(Description = "응답 유실 후 보이는 잔액으로 구매를 확정하지 않고 동일 POST 본문을 재전송한다")]
        public void LostBuyResponse_RetriesSameRequestAndRequiresReceipt()
        {
            var request = ShopExchangeRequest.Buy(State(), 0, "buy-one");
            var exchange = new HttpShopExchange(_api, request);
            _transport.Queued.Enqueue(HttpResponseData.Failed(HttpFailure.Timeout));
            _transport.Queued.Enqueue(Ok(Account(40, 5, Daily(purchased: true))));
            var unknown = exchange.Submit().GetAwaiter().GetResult();
            Assert.AreEqual(ShopSubmissionStatus.Unknown, unknown.Status);
            Assert.IsNull(unknown.DailyReceipt);
            Assert.AreEqual(5, unknown.Account.Gems);

            _transport.Queued.Enqueue(Ok(DailyReceipt("buy-one", "BUY", 0, Daily(purchased: true))));
            _transport.Queued.Enqueue(Ok(Account(40, 5, Daily(purchased: true))));
            var confirmed = exchange.Submit().GetAwaiter().GetResult();
            Assert.AreEqual(ShopSubmissionStatus.Acknowledged, confirmed.Status);
            Assert.AreEqual(7, confirmed.DailyReceipt.GemsSpent);
            Assert.AreEqual(0, confirmed.DailyReceipt.Slot);
            var posts = _transport.Requests.Where(r => r.Verb == HttpVerb.Post).ToArray();
            Assert.AreEqual(posts[0].Body, posts[1].Body);
            CollectionAssert.AreEquivalent(new[] { "requestId", "dayKey", "rerollIndex", "slot" },
                JObject.Parse(posts[0].Body).Properties().Select(x => x.Name));
        }

        [Test(Description = "영수증 확정 뒤 상태 GET 실패는 POST를 반복하지 않는다")]
        public void ConfirmedReroll_GetFailure_RetriesOnlyGet()
        {
            var exchange = new HttpShopExchange(_api, ShopExchangeRequest.GoldReroll(State(), "roll-one"));
            _transport.Queued.Enqueue(Ok(DailyReceipt("roll-one", "GOLD_REROLL", null, Daily(reroll: 1))));
            _transport.Queued.Enqueue(new HttpResponseData(500, ""));
            Assert.Throws<ApiException>(() => exchange.Submit().GetAwaiter().GetResult());
            Assert.AreEqual(ShopSubmissionStatus.Acknowledged, exchange.Status);

            _transport.Queued.Enqueue(Ok(Account(37, 12, Daily(reroll: 1))));
            var result = exchange.Submit().GetAwaiter().GetResult();
            Assert.AreEqual(3, result.DailyReceipt.GoldSpent);
            Assert.AreEqual(1, result.Account.Daily.RerollIndex);
            Assert.AreEqual(1, _transport.Requests.Count(r => r.Verb == HttpVerb.Post));
        }

        [Test(Description = "오래된 화면의 명확한 409 거절은 새 요청 ID로 자동 반복하지 않는다")]
        public void StaleDay_RejectedWithoutAnotherPost()
        {
            var exchange = new HttpShopExchange(_api, ShopExchangeRequest.Buy(State(), 0, "old-day"));
            _transport.Queued.Enqueue(new HttpResponseData(409, ""));
            _transport.Queued.Enqueue(Ok(Account(40, 12, Daily(reroll: 1))));
            var result = exchange.Submit().GetAwaiter().GetResult();
            Assert.AreEqual(ShopSubmissionStatus.Rejected, result.Status);
            Assert.AreEqual("HTTP_409", result.ErrorCode);

            _transport.Queued.Enqueue(Ok(Account(40, 12, Daily(reroll: 1))));
            Assert.AreEqual(ShopSubmissionStatus.Rejected, exchange.Submit().GetAwaiter().GetResult().Status);
            Assert.AreEqual(1, _transport.Requests.Count(r => r.Verb == HttpVerb.Post));
        }

        [Test(Description = "골드팩 가격은 서버 카탈로그를 표시하고 전송에는 버전·인덱스·키만 넣는다")]
        public void GoldPack_BuysQuotedVersion_WithoutSendingPrice()
        {
            _transport.Queued.Enqueue(Ok(Packs()));
            var catalog = _api.GetGoldPacks().GetAwaiter().GetResult();
            var exchange = new HttpShopExchange(_api, ShopExchangeRequest.GoldPack(catalog, 0, "pack-one"));
            _transport.Queued.Enqueue(Ok(GoldReceipt("pack-one")));
            _transport.Queued.Enqueue(Ok(Account(141, 3, Daily())));
            var result = exchange.Submit().GetAwaiter().GetResult();
            Assert.AreEqual(ShopSubmissionStatus.Acknowledged, result.Status);
            Assert.AreEqual(101, result.GoldPackReceipt.Gold);
            Assert.AreEqual(9, result.GoldPackReceipt.GemsSpent);
            Assert.AreEqual(141, result.Account.Gold);
            CollectionAssert.AreEquivalent(new[] { "requestId", "configVersion", "index" },
                JObject.Parse(_transport.Requests[1].Body).Properties().Select(x => x.Name));
        }

        [Test(Description = "성공 HTTP이지만 다른 슬롯의 영수증이면 미확정으로 남겨 같은 키를 재요청한다")]
        public void ChangedReceipt_IsUnknownUntilOriginalReceiptArrives()
        {
            var exchange = new HttpShopExchange(_api, ShopExchangeRequest.Buy(State(), 0, "wrong-slot"));
            _transport.Queued.Enqueue(Ok(DailyReceipt("wrong-slot", "BUY", 1, Daily(purchased: true))));
            _transport.Queued.Enqueue(Ok(Account(40, 5, Daily(purchased: true))));
            Assert.AreEqual(ShopSubmissionStatus.Unknown, exchange.Submit().GetAwaiter().GetResult().Status);

            _transport.Queued.Enqueue(Ok(DailyReceipt("wrong-slot", "BUY", 0, Daily(purchased: true))));
            _transport.Queued.Enqueue(Ok(Account(40, 5, Daily(purchased: true))));
            Assert.AreEqual(ShopSubmissionStatus.Acknowledged, exchange.Submit().GetAwaiter().GetResult().Status);
        }

        [Test(Description = "계정 교체는 이전 요청의 재전송과 이전 계정의 응답 적용을 막는다")]
        public void SessionSwitch_BlocksOldExchange()
        {
            var exchange = new HttpShopExchange(_api, ShopExchangeRequest.GoldReroll(State(), "old-account"));
            _tokens.Set("account-b", "refresh-b");
            var error = Assert.Throws<ApiException>(() => exchange.Submit().GetAwaiter().GetResult());
            Assert.AreEqual(HttpErrorMapper.SessionExpired, error.Code);
            Assert.That(_transport.Requests.All(r => r.Verb == HttpVerb.Get));
        }

        [Test(Description = "누락된 계정 상점과 중복 JSON 필드는 복원 결과로 받아들이지 않는다")]
        public void InvalidRestoration_DoesNotInventEmptyShop()
        {
            _transport.Queued.Enqueue(Ok("{\"gold\":40,\"gem\":12}"));
            Assert.AreEqual(HttpErrorMapper.InvalidResponse,
                Assert.Throws<ApiException>(() => _api.GetAccount().GetAwaiter().GetResult()).Code);
            _transport.Queued.Enqueue(Ok("{\"available\":false,\"available\":true}"));
            Assert.AreEqual(HttpErrorMapper.InvalidResponse,
                Assert.Throws<ApiException>(() => _api.GetDaily().GetAwaiter().GetResult()).Code);
            Assert.AreEqual(ShopSubmissionStatus.NotSubmitted,
                new HttpShopExchange(_api, ShopExchangeRequest.Buy(State(), 0, "fresh")).Status);
        }

        [Test(Description = "광고 공급자와 서버 검증 전에는 광고 재추첨 요청을 전송하지 않는다")]
        public void AdReroll_IsUnavailableWithoutVerification()
        {
            var error = Assert.Throws<ApiException>(() => ShopExchangeRequest.AdReroll());
            Assert.AreEqual("SHOP_AD_UNAVAILABLE", error.Code);
            Assert.IsEmpty(_transport.Requests);
        }

        [Test(Description = "응답 유실 뒤 재시작해도 동일 계정의 동일 요청 ID와 견적을 재전송하고 영수증 확인 후 삭제한다")]
        public void PendingBuy_SurvivesRestart_WithoutSecondPurchase()
        {
            string directory = TemporaryDirectory();
            try
            {
                _tokens.Set(Token(41), "refresh-a");
                _api = new HttpShopApi(new HttpApiConfig("https://example.test"), _transport, _tokens);
                var journal = new ShopPendingRequestStore(directory, _tokens);
                var exchange = journal.Begin(_api, ShopExchangeRequest.Buy(State(), 0, "saved-buy"));
                _transport.Queued.Enqueue(HttpResponseData.Failed(HttpFailure.Timeout));
                _transport.Queued.Enqueue(Ok(Account(40, 5, Daily(purchased: true))));
                Assert.AreEqual(ShopSubmissionStatus.Unknown, exchange.Submit().GetAwaiter().GetResult().Status);
                Assert.AreEqual(1, Directory.GetFiles(directory, "shop-pending-*.json").Length);
                Assert.Throws<InvalidOperationException>(() =>
                    journal.Begin(_api, ShopExchangeRequest.GoldReroll(State(), "second-key")));

                var otherAccount = new AuthSession();
                otherAccount.Set(Token(42), "refresh-b");
                var otherApi = new HttpShopApi(new HttpApiConfig("https://example.test"), _transport, otherAccount);
                Assert.IsNull(new ShopPendingRequestStore(directory, otherAccount).Recover(otherApi));

                var restarted = new AuthSession();
                restarted.Set(Token(41), "refresh-new-device");
                var api = new HttpShopApi(new HttpApiConfig("https://example.test"), _transport, restarted);
                var recovered = new ShopPendingRequestStore(directory, restarted).Recover(api);
                Assert.AreEqual(ShopSubmissionStatus.Unknown, recovered.Status);
                _transport.Queued.Enqueue(Ok(DailyReceipt("saved-buy", "BUY", 0, Daily(purchased: true))));
                _transport.Queued.Enqueue(Ok(Account(40, 5, Daily(purchased: true))));
                var result = recovered.Submit().GetAwaiter().GetResult();
                Assert.AreEqual(ShopSubmissionStatus.Acknowledged, result.Status);
                Assert.AreEqual(7, result.DailyReceipt.GemsSpent);
                Assert.IsNull(new ShopPendingRequestStore(directory, restarted).Recover(api));
                var posts = _transport.Requests.Where(r => r.Verb == HttpVerb.Post).ToArray();
                Assert.AreEqual(2, posts.Length);
                Assert.AreEqual(posts[0].Body, posts[1].Body);
            }
            finally { Directory.Delete(directory, true); }
        }

        [Test(Description = "첫 요청의 확정 거절은 저장 요청을 제거하고 새 요청을 허용한다")]
        public void DefiniteRejection_ClearsPendingRequest()
        {
            string directory = TemporaryDirectory();
            try
            {
                _tokens.Set(Token(41), "refresh-a");
                _api = new HttpShopApi(new HttpApiConfig("https://example.test"), _transport, _tokens);
                var journal = new ShopPendingRequestStore(directory, _tokens);
                var exchange = journal.Begin(_api, ShopExchangeRequest.Buy(State(), 0, "rejected-buy"));
                _transport.Queued.Enqueue(new HttpResponseData(409, ""));
                _transport.Queued.Enqueue(Ok(Account(40, 12, Daily())));
                Assert.AreEqual(ShopSubmissionStatus.Rejected, exchange.Submit().GetAwaiter().GetResult().Status);
                Assert.IsNull(journal.Recover(_api));
            }
            finally { Directory.Delete(directory, true); }
        }

        [Test(Description = "POST 확정 뒤 계정 조회가 실패하면 영수증을 보존하고 재시작 시 같은 요청 키로 복구한다")]
        public void AcknowledgedReceipt_KeepsJournalUntilAccountRead()
        {
            string directory = TemporaryDirectory();
            try
            {
                _tokens.Set(Token(41), "refresh-a");
                _api = new HttpShopApi(new HttpApiConfig("https://example.test"), _transport, _tokens);
                var journal = new ShopPendingRequestStore(directory, _tokens);
                var exchange = journal.Begin(_api, ShopExchangeRequest.Buy(State(), 0, "buy-saved"));
                _transport.Queued.Enqueue(Ok(DailyReceipt("buy-saved", "BUY", 0, Daily(purchased: true))));
                _transport.Queued.Enqueue(new HttpResponseData(500, ""));
                Assert.Throws<ApiException>(() => exchange.Submit().GetAwaiter().GetResult());
                Assert.AreEqual(ShopSubmissionStatus.Acknowledged, exchange.Status);
                Assert.AreEqual(1, Directory.GetFiles(directory, "shop-pending-*.json").Length);
                Assert.AreEqual(1, _transport.Requests.Count(r => r.Verb == HttpVerb.Post));

                var restarted = new AuthSession();
                restarted.Set(Token(41), "refresh-new-device");
                var api = new HttpShopApi(new HttpApiConfig("https://example.test"), _transport, restarted);
                var recovered = new ShopPendingRequestStore(directory, restarted).Recover(api);
                _transport.Queued.Enqueue(Ok(DailyReceipt("buy-saved", "BUY", 0, Daily(purchased: true))));
                _transport.Queued.Enqueue(Ok(Account(40, 5, Daily(purchased: true))));
                var result = recovered.Submit().GetAwaiter().GetResult();
                Assert.AreEqual(ShopSubmissionStatus.Acknowledged, result.Status);
                Assert.AreEqual(7, result.DailyReceipt.GemsSpent);
                Assert.IsNull(new ShopPendingRequestStore(directory, restarted).Recover(api));
                var posts = _transport.Requests.Where(r => r.Verb == HttpVerb.Post).ToArray();
                Assert.AreEqual(2, posts.Length);
                Assert.AreEqual(posts[0].Body, posts[1].Body);
            }
            finally { Directory.Delete(directory, true); }
        }

        [Test(Description = "확정된 구매의 계정 조회만 실패하면 새 구매를 막고 GET 재시도로 영수증을 표시할 수 있다")]
        public void ViewSession_AcknowledgedBuyRetriesAccountAndReturnsReceipt()
        {
            string directory = TemporaryDirectory();
            try
            {
                _tokens.Set(Token(41), "refresh-a");
                _api = new HttpShopApi(new HttpApiConfig("https://example.test"), _transport, _tokens);
                var session = new ServerShopViewSession(_api, new ShopPendingRequestStore(directory, _tokens),
                    new PlayerProfile(new GameDataStore()));
                _transport.Queued.Enqueue(Ok(Account(40, 12, Daily())));
                _transport.Queued.Enqueue(Ok(Packs()));
                session.Restore().GetAwaiter().GetResult();

                // 고정 요청 ID는 실행 시 생성된다. 이 테스트에서는 POST 응답이 원래 키를 되돌려 준다.
                _transport.OnSend = request => request.Verb == HttpVerb.Post
                    ? Ok(DailyReceipt(JObject.Parse(request.Body).Value<string>("requestId"), "BUY", 0,
                        Daily(purchased: true))) : (HttpResponseData?)null;
                _transport.Queued.Enqueue(new HttpResponseData(500, ""));
                Assert.Throws<ApiException>(() => session.BuyDaily(session.Account.Daily, 0).GetAwaiter().GetResult());
                Assert.IsTrue(session.HasPending);
                Assert.IsNull(session.Account);
                Assert.AreEqual(1, Directory.GetFiles(directory, "shop-pending-*.json").Length);

                _transport.Queued.Enqueue(Ok(Account(40, 5, Daily(purchased: true))));
                _transport.Queued.Enqueue(new HttpResponseData(500, ""));
                var restored = session.Restore().GetAwaiter().GetResult();
                Assert.AreEqual(ShopViewSubmissionStatus.Acknowledged, restored.Status);
                Assert.IsTrue(restored.IsPurchase);
                Assert.AreEqual(1, restored.Rewards.Count);
                Assert.AreEqual(1, _transport.Requests.Count(r => r.Verb == HttpVerb.Post));
                Assert.IsFalse(session.HasPending);
                Assert.IsNull(session.GoldPacks);

                _transport.Queued.Enqueue(Ok(Account(40, 5, Daily(purchased: true))));
                _transport.Queued.Enqueue(Ok(Packs()));
                Assert.IsNull(session.Restore().GetAwaiter().GetResult());
                Assert.IsNotNull(session.GoldPacks);
            }
            finally { Directory.Delete(directory, true); }
        }

        [Test(Description = "골드팩 목록만 실패하면 일일 상점 계정 상태는 유지하고 목록만 재시도한다")]
        public void ViewSession_PackFailureKeepsDailyAccount()
        {
            string directory = TemporaryDirectory();
            try
            {
                _tokens.Set(Token(41), "refresh-a");
                _api = new HttpShopApi(new HttpApiConfig("https://example.test"), _transport, _tokens);
                var session = new ServerShopViewSession(_api, new ShopPendingRequestStore(directory, _tokens),
                    new PlayerProfile(new GameDataStore()));
                _transport.Queued.Enqueue(Ok(Account(40, 12, Daily())));
                _transport.Queued.Enqueue(new HttpResponseData(500, ""));
                Assert.IsNull(session.Restore().GetAwaiter().GetResult());
                Assert.IsNotNull(session.Account);
                Assert.AreEqual(40, session.Account.Gold);
                Assert.IsNull(session.GoldPacks);

                _transport.Queued.Enqueue(Ok(Account(40, 12, Daily())));
                _transport.Queued.Enqueue(Ok(Packs()));
                Assert.IsNull(session.Restore().GetAwaiter().GetResult());
                Assert.IsNotNull(session.GoldPacks);
            }
            finally { Directory.Delete(directory, true); }
        }

        [Test(Description = "골드팩 조회 중 계정이 바뀌면 이전 계정의 일일 상점 상태를 버린다")]
        public void ViewSession_SessionChangeDuringPackReadClearsAccount()
        {
            string directory = TemporaryDirectory();
            try
            {
                _tokens.Set(Token(41), "refresh-a");
                _api = new HttpShopApi(new HttpApiConfig("https://example.test"), _transport, _tokens);
                var session = new ServerShopViewSession(_api, new ShopPendingRequestStore(directory, _tokens),
                    new PlayerProfile(new GameDataStore()));
                _transport.Queued.Enqueue(Ok(Account(40, 12, Daily())));
                _transport.OnSend = request =>
                {
                    if (!request.Url.EndsWith("/gold-packs", StringComparison.Ordinal)) { return null; }
                    _tokens.Set(Token(42), "refresh-b");
                    return Ok(Packs());
                };

                var error = Assert.Throws<ApiException>(() => session.Restore().GetAwaiter().GetResult());
                Assert.AreEqual(HttpErrorMapper.SessionExpired, error.Code);
                Assert.IsNull(session.Account);
                Assert.IsNull(session.GoldPacks);
            }
            finally { Directory.Delete(directory, true); }
        }

        [Test(Description = "대기 요청을 디스크에 쓸 수 없으면 POST를 보내지 않는다")]
        public void PersistenceFailure_DoesNotSendPurchase()
        {
            string directory = TemporaryDirectory();
            try
            {
                _tokens.Set(Token(41), "refresh-a");
                _api = new HttpShopApi(new HttpApiConfig("https://example.test"), _transport, _tokens);
                string occupiedPath = Path.Combine(directory, "occupied");
                File.WriteAllText(occupiedPath, "file");
                var journal = new ShopPendingRequestStore(occupiedPath, _tokens);
                var exchange = journal.Begin(_api, ShopExchangeRequest.Buy(State(), 0, "cannot-save"));
                Assert.Throws<IOException>(() => exchange.Submit().GetAwaiter().GetResult());
                Assert.IsFalse(_transport.Requests.Any(r => r.Verb == HttpVerb.Post));
            }
            finally { Directory.Delete(directory, true); }
        }

        private static string TemporaryDirectory()
        {
            string path = Path.Combine(Path.GetTempPath(), "shop-pending-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private static string Token(long id)
        {
            string payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(new JObject
            { ["sub"] = id.ToString() }.ToString(Formatting.None))).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            return "header." + payload + ".signature";
        }

        private ServerDailyShop State()
        {
            _transport.Queued.Enqueue(Ok(Daily()));
            return _api.GetDaily().GetAwaiter().GetResult();
        }

        private static HttpResponseData Ok(string body) => new(200, body);

        private static string Account(int gold, int gems, string daily)
        {
            var account = JObject.Parse(HttpAccountProgressTests.State);
            account["gold"] = gold;
            account["gem"] = gems;
            account["shop"] = JObject.Parse(daily);
            return account.ToString(Formatting.None);
        }

        private static string Daily(bool purchased = false, int reroll = 0) => new JObject
        {
            ["available"] = true,
            ["dayKey"] = "20261019",
            ["nextResetAt"] = "2026-10-19T19:00:00Z",
            ["configVersion"] = 2,
            ["rerollIndex"] = reroll,
            ["goldUsed"] = reroll,
            ["goldLeft"] = 2 - reroll,
            ["goldRerollCost"] = 3,
            ["adUsed"] = 0,
            ["adAvailable"] = false,
            ["purchased"] = purchased ? new JArray(0) : new JArray(),
            ["offers"] = new JArray(new JObject
            {
                ["slot"] = 0,
                ["discountPercent"] = 30,
                ["price"] = 7,
                ["offer"] = new JObject
                {
                    ["id"] = "offer.synthetic",
                    ["price"] = 10,
                    ["gold"] = 0,
                    ["energy"] = 0,
                    ["items"] = new JObject { ["book.arrow"] = 1 }
                }
            })
        }.ToString(Formatting.None);

        private static string Packs() => new JObject
        {
            ["available"] = true,
            ["version"] = 3,
            ["gemGoldRate"] = 11,
            ["packs"] = new JArray(new JObject
            { ["index"] = 0, ["gold"] = 101, ["discountPercent"] = 7, ["gemPrice"] = 9 })
        }.ToString(Formatting.None);

        private static string DailyReceipt(string key, string operation, int? slot, string stateAfter) => new JObject
        {
            ["requestId"] = key,
            ["operation"] = operation,
            ["dayKey"] = "20261019",
            ["requestedReroll"] = 0,
            ["slot"] = slot.HasValue ? new JValue(slot.Value) : JValue.CreateNull(),
            ["goldSpent"] = operation == "GOLD_REROLL" ? 3 : 0,
            ["gemsSpent"] = operation == "BUY" ? 7 : 0,
            ["gold"] = 0,
            ["energy"] = 0,
            ["items"] = operation == "BUY" ? new JObject { ["book.arrow"] = 1 } : new JObject(),
            ["stateAfter"] = JObject.Parse(stateAfter),
            ["completedAt"] = "2026-10-19T01:02:03.123456789Z"
        }.ToString(Formatting.None);

        private static string GoldReceipt(string key) => new JObject
        {
            ["requestId"] = key,
            ["configVersion"] = 3,
            ["index"] = 0,
            ["gold"] = 101,
            ["gemsSpent"] = 9,
            ["gemGoldRate"] = 11,
            ["discountPercent"] = 7,
            ["completedAt"] = "2026-10-19T01:02:03.123456789Z"
        }.ToString(Formatting.None);
    }
}
