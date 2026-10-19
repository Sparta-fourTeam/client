using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;

namespace Game.Network
{
    /// <summary>두 상점 탭이 공유하는 서버 상태. 확정 전 요청은 새 구매보다 먼저 같은 ID로 복구한다.</summary>
    public sealed class ServerShopViewSession : IServerShopViewSession
    {
        private readonly HttpShopApi _api;
        private readonly ShopPendingRequestStore _pendingStore;
        private HttpShopExchange _pending;
        private ServerShopAccount _account;
        private ServerGoldPackCatalog _packs;
        private int _busy;

        public ShopViewAccount Account { get; private set; }
        public ShopViewCatalog GoldPacks { get; private set; }
        public bool IsBusy => _busy != 0;
        public bool HasPending => _pending != null;
        public event Action Changed;

        public ServerShopViewSession(HttpShopApi api, ShopPendingRequestStore pendingStore)
        {
            _api = api ?? throw new ArgumentNullException(nameof(api));
            _pendingStore = pendingStore ?? throw new ArgumentNullException(nameof(pendingStore));
        }

        /// <summary>로그인/화면 재진입 시 저장된 요청부터 재전송하고 현재 계정·카탈로그를 읽는다.</summary>
        public async UniTask<ShopViewResult> Restore()
        {
            Enter();
            try
            {
                _pending ??= _pendingStore.Recover(_api);
                ShopReconciliationResult result = null;
                if (_pending != null)
                {
                    result = await _pending.Submit();
                    Apply(result.Account);
                    if (result.Status != ShopSubmissionStatus.Unknown) { _pending = null; }
                }
                else { Apply(await _api.GetAccount()); }
                _packs = await _api.GetGoldPacks();
                GoldPacks = Map(_packs);
                return Map(result);
            }
            catch
            {
                // 영수증만 확인되고 GET이 실패해도 오래된 진열로 새 구매를 시작하지 않는다.
                _account = null;
                Account = null;
                if (_pending?.Status == ShopSubmissionStatus.Rejected)
                { _pending = null; }
                throw;
            }
            finally { Leave(); }
        }

        public UniTask<ShopViewResult> BuyDaily(ShopViewDay quoted, int slot) =>
            Execute(quoted == Account?.Daily ? ShopExchangeRequest.Buy(_account.Daily, slot, Guid.NewGuid().ToString("N"))
                : throw new InvalidOperationException("서버 상점 진열을 다시 확인해야 합니다"));

        public UniTask<ShopViewResult> GoldReroll(ShopViewDay quoted) =>
            Execute(quoted == Account?.Daily ? ShopExchangeRequest.GoldReroll(_account.Daily, Guid.NewGuid().ToString("N"))
                : throw new InvalidOperationException("서버 상점 진열을 다시 확인해야 합니다"));

        public UniTask<ShopViewResult> BuyGoldPack(ShopViewCatalog quoted, int index) =>
            Execute(Account != null && quoted == GoldPacks
                ? ShopExchangeRequest.GoldPack(_packs, index, Guid.NewGuid().ToString("N"))
                : throw new InvalidOperationException("서버 골드팩 가격을 다시 확인해야 합니다"));

        public async UniTask<ShopViewResult> RetryPending()
        {
            Enter();
            try
            {
                _pending ??= _pendingStore.Recover(_api);
                if (_pending == null || _pending.Status == ShopSubmissionStatus.Rejected)
                { throw new InvalidOperationException("미확정 상점 요청이 없습니다"); }
                return Map(await SubmitPending());
            }
            finally { Leave(); }
        }

        private async UniTask<ShopViewResult> Execute(ShopExchangeRequest request)
        {
            Enter();
            try
            {
                if (_pending != null) { throw new InvalidOperationException("미확정 상점 요청을 먼저 복구해야 합니다"); }
                _pending = _pendingStore.Begin(_api, request);
                return Map(await SubmitPending());
            }
            finally { Leave(); }
        }

        private async UniTask<ShopReconciliationResult> SubmitPending()
        {
            try
            {
                var result = await _pending.Submit();
                Apply(result.Account);
                if (result.Status != ShopSubmissionStatus.Unknown) { _pending = null; }
                return result;
            }
            catch
            {
                _account = null;
                Account = null;
                if (_pending?.Status == ShopSubmissionStatus.Rejected)
                { _pending = null; }
                throw;
            }
        }

        private void Apply(ServerShopAccount account)
        {
            _account = account;
            Account = new ShopViewAccount(account.Gold, account.Gems, Map(account.Daily));
        }

        private static ShopViewDay Map(ServerDailyShop day) => new(day.Available, day.NextResetAt,
            day.GoldUsed, day.GoldLeft, day.GoldRerollCost, day.Purchased,
            day.Offers.Select(offer => new ShopViewOffer(offer.Price, offer.DiscountPercent,
                Rewards(offer.Gold, offer.Energy, offer.Items))).ToArray());

        private static ShopViewCatalog Map(ServerGoldPackCatalog catalog) => new(catalog.Available,
            catalog.Packs.Select(pack => new ShopViewPack(pack.Gold, pack.GemPrice, pack.DiscountPercent)).ToArray());

        private static IReadOnlyList<ShopViewReward> Rewards(int gold, int energy, IReadOnlyDictionary<string, int> items)
        {
            var result = new List<ShopViewReward>();
            if (gold > 0) { result.Add(new ShopViewReward(ShopRules.CoinReward, gold)); }
            if (energy > 0) { result.Add(new ShopViewReward(ShopRules.EnergyReward, energy)); }
            result.AddRange(items.OrderBy(row => row.Key, StringComparer.Ordinal)
                .Select(row => new ShopViewReward(row.Key, row.Value)));
            return result;
        }

        private static ShopViewResult Map(ShopReconciliationResult result)
        {
            if (result == null) { return null; }
            IReadOnlyList<ShopViewReward> rewards = result.DailyReceipt != null
                ? Rewards(result.DailyReceipt.Gold, result.DailyReceipt.Energy, result.DailyReceipt.Items)
                : result.GoldPackReceipt != null
                    ? new[] { new ShopViewReward(ShopRules.CoinReward, result.GoldPackReceipt.Gold) }
                    : Array.Empty<ShopViewReward>();
            return new ShopViewResult(result.Status switch
            {
                ShopSubmissionStatus.Acknowledged => ShopViewSubmissionStatus.Acknowledged,
                ShopSubmissionStatus.Unknown => ShopViewSubmissionStatus.Unknown,
                _ => ShopViewSubmissionStatus.Rejected
            }, result.ErrorCode, rewards,
                result.GoldPackReceipt != null || result.DailyReceipt?.Operation == "BUY");
        }

        private void Enter()
        {
            if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
            { throw new InvalidOperationException("상점 요청이 이미 진행 중입니다"); }
            Changed?.Invoke();
        }

        private void Leave()
        {
            Interlocked.Exchange(ref _busy, 0);
            Changed?.Invoke();
        }
    }
}
