using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;

namespace Game.Network
{
    /// <summary>서버에서 함께 읽은 현재 지갑·일일 진열과 별도 버전의 골드팩 카탈로그.</summary>
    public sealed class ServerShopRestoration
    {
        public ServerShopAccount Account { get; }
        public ServerGoldPackCatalog GoldPacks { get; }

        internal ServerShopRestoration(ServerShopAccount account, ServerGoldPackCatalog packs)
        { Account = account; GoldPacks = packs; }
    }

    /// <summary>조회 당시 날짜·회차 또는 골드팩 버전과 요청 ID를 고정한다. 재전송 때 새 요청을 만들지 않는다.</summary>
    public sealed class ShopExchangeRequest
    {
        public string RequestId { get; }
        public string Operation { get; }
        public string DayKey { get; }
        public int RerollIndex { get; }
        public int? Slot { get; }
        public int ConfigVersion { get; }
        public int Index { get; }

        private ShopExchangeRequest(string key, string operation, string day, int reroll, int? slot, int version, int index)
        {
            RequestId = ValidKey(key); Operation = operation; DayKey = day; RerollIndex = reroll;
            Slot = slot; ConfigVersion = version; Index = index;
        }

        public static ShopExchangeRequest Buy(ServerDailyShop daily, int slot, string requestId)
        {
            if (daily == null || !daily.Available || slot < 0 || slot >= daily.Offers.Count)
            { throw new ArgumentException("조회한 서버 진열과 슬롯이 필요합니다", nameof(daily)); }
            return new ShopExchangeRequest(requestId, "BUY", daily.DayKey, daily.RerollIndex, slot, 0, 0);
        }

        public static ShopExchangeRequest GoldReroll(ServerDailyShop daily, string requestId)
        {
            if (daily == null || !daily.Available)
            { throw new ArgumentException("조회한 서버 진열이 필요합니다", nameof(daily)); }
            return new ShopExchangeRequest(requestId, "GOLD_REROLL", daily.DayKey, daily.RerollIndex, null, 0, 0);
        }

        public static ShopExchangeRequest GoldPack(ServerGoldPackCatalog catalog, int index, string requestId)
        {
            if (catalog == null || !catalog.Available || index < 0 || index >= catalog.Packs.Count)
            { throw new ArgumentException("조회한 서버 골드팩이 필요합니다", nameof(catalog)); }
            return new ShopExchangeRequest(requestId, "GOLD_PACK", null, 0, null, catalog.Version, index);
        }

        /// <summary>광고 공급자·서버 검증이 도입되기 전에는 AD 작업을 만들지 않는다.</summary>
        public static ShopExchangeRequest AdReroll() =>
            throw new ApiException(ApiErrorKind.Rejected, "SHOP_AD_UNAVAILABLE");

        private static string ValidKey(string key)
        {
            if (string.IsNullOrEmpty(key) || key.Length > 64) { throw new ArgumentException("요청 ID가 필요합니다", nameof(key)); }
            foreach (char c in key)
            {
                if (!((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')
                    || c == '_' || c == '-')) { throw new ArgumentException("요청 ID 형식이 올바르지 않습니다", nameof(key)); }
            }
            return key;
        }
    }

    /// <summary>서버 #63의 조회·구매 계약. 계정이 바뀐 작업은 전송·적용을 막고 가격·보상은 서버 응답만 사용한다.</summary>
    public sealed class HttpShopApi
    {
        private readonly HttpApiClient _client;
        private readonly IAuthTokenStore _tokens;
        private readonly long _generation;

        public HttpShopApi(HttpApiConfig config, IHttpTransport transport, IAuthTokenStore tokens)
        {
            _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
            _generation = tokens.Generation;
            EnsureSession();
            _client = new HttpApiClient(config, new SessionTransport(transport, EnsureSession), tokens: tokens);
        }

        /// <summary>새 인증 세션이나 다른 기기에서 확정된 지갑·진열을 복원한다.</summary>
        public async UniTask<ServerShopRestoration> Restore(CancellationToken cancellationToken = default)
        {
            var account = await GetAccount(cancellationToken);
            var packs = await GetGoldPacks(cancellationToken);
            return new ServerShopRestoration(account, packs);
        }

        public async UniTask<ServerShopAccount> GetAccount(CancellationToken cancellationToken = default)
        {
            EnsureSession();
            string json = await _client.GetJson("api/user/me", cancellationToken);
            EnsureSession();
            return ServerShopContract.Account(json);
        }

        public async UniTask<ServerDailyShop> GetDaily(CancellationToken cancellationToken = default)
        {
            EnsureSession();
            string json = await _client.GetJson("api/user/shop", cancellationToken);
            EnsureSession();
            return ServerShopContract.Daily(json);
        }

        public async UniTask<ServerGoldPackCatalog> GetGoldPacks(CancellationToken cancellationToken = default)
        {
            EnsureSession();
            string json = await _client.GetJson("api/user/shop/gold-packs", cancellationToken);
            EnsureSession();
            return ServerShopContract.GoldPacks(json);
        }

        internal async UniTask<(ServerDailyShopReceipt daily, ServerGoldPackReceipt pack)> Post(
            ShopExchangeRequest request, CancellationToken cancellationToken)
        {
            EnsureSession();
            string json;
            switch (request.Operation)
            {
                case "BUY":
                    json = await _client.PostJson("api/user/shop/buy", new
                    {
                        requestId = request.RequestId,
                        dayKey = request.DayKey,
                        rerollIndex = request.RerollIndex,
                        slot = request.Slot.Value
                    }, cancellationToken);
                    EnsureSession();
                    return (ServerShopContract.DailyReceipt(json, request), null);
                case "GOLD_REROLL":
                    json = await _client.PostJson("api/user/shop/reroll", new
                    {
                        requestId = request.RequestId,
                        dayKey = request.DayKey,
                        rerollIndex = request.RerollIndex,
                        kind = "GOLD"
                    }, cancellationToken);
                    EnsureSession();
                    return (ServerShopContract.DailyReceipt(json, request), null);
                case "GOLD_PACK":
                    json = await _client.PostJson("api/user/shop/gold-packs/buy", new
                    { requestId = request.RequestId, configVersion = request.ConfigVersion, index = request.Index }, cancellationToken);
                    EnsureSession();
                    return (null, ServerShopContract.GoldPackReceipt(json, request));
                default:
                    throw new ArgumentOutOfRangeException(nameof(request));
            }
        }

        internal void EnsureSession()
        {
            if (_tokens.Generation != _generation || string.IsNullOrEmpty(_tokens.AccessToken))
            { throw new ApiException(ApiErrorKind.Rejected, HttpErrorMapper.SessionExpired); }
        }

        private sealed class SessionTransport : IHttpTransport
        {
            private readonly IHttpTransport _transport;
            private readonly Action _ensure;

            public SessionTransport(IHttpTransport transport, Action ensure)
            { _transport = transport ?? throw new ArgumentNullException(nameof(transport)); _ensure = ensure; }

            public UniTask<HttpResponseData> Send(HttpRequestData request, float timeoutSeconds, CancellationToken cancellationToken)
            { _ensure(); return _transport.Send(request, timeoutSeconds, cancellationToken); }
        }
    }
}
