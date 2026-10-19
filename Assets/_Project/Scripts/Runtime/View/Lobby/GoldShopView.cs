using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Core;
using UnityEngine;
using VContainer;

namespace Game.View
{
    /// <summary>
    /// 상점 화면의 골드 탭(코인 상점). 잼으로 코인 묶음을 산다. 가격은 잼 1개당 코인 기준 비율로 정해지고,
    /// 큰 묶음일수록 할인이 붙는다(ShopConfig.GoldPacks). 잼이 모자라도 누를 수 있고, 확인한 뒤에 서버가 거절하면 그때 알려 준다
    /// </summary>
    public sealed class GoldShopView : MonoBehaviour, IUiBindable
    {
        [SerializeField] private DailyShopItemView[] _items;
        [SerializeField] private ItemIconTable _itemIcons;

        private PlayerProfile _profile;
        private GameDataStore _data;
        private IShopApi _api;
        private RewardPopupView _rewardPopup;
        private DetailPopupView _detail;
        private ConfirmPopupView _confirm;
        private bool _busy;
        private bool _refreshing;
        private float _nextServerRefresh;
        private IServerShopViewSession _server;

        /// <summary>서버 카탈로그를 명시적으로 주입한다. 기본 Local 상점 DI는 그대로 둔다.</summary>
        public void UseServerShop(IServerShopViewSession session)
        {
            if (_server != null && isActiveAndEnabled) { _server.Changed -= Refresh; }
            _server = session ?? throw new ArgumentNullException(nameof(session));
            if (isActiveAndEnabled) { _server.Changed += Refresh; }
            Refresh();
            if (isActiveAndEnabled) { RefreshServerAsync().Forget(Debug.LogException); }
        }

        public void BindUi(IUiRegistry registry)
        {
            _rewardPopup = registry.Get<RewardPopupView>();
            _confirm = registry.Get<ConfirmPopupView>();
            _detail = registry.Get<DetailPopupView>();
        }

        [Inject]
        public void Construct(PlayerProfile profile, GameDataStore data, IShopApi api)
        {
            _profile = profile;
            _data = data;
            _api = api;
            Refresh();
        }

        private void OnEnable()
        {
            if (_profile == null) { return; }
            if (_server != null) { _server.Changed += Refresh; }
            Refresh();
            if (_server != null) { RefreshServerAsync().Forget(Debug.LogException); }
        }

        private void OnDisable()
        {
            if (_server != null) { _server.Changed -= Refresh; }
        }

        private void Update()
        {
            if (_server == null || _profile == null || Time.unscaledTime < _nextServerRefresh
                || (!_server.HasPending && _server.Account != null && _server.GoldPacks != null)) { return; }
            _nextServerRefresh = Time.unscaledTime + 10f;
            RefreshServerAsync().Forget(Debug.LogException);
        }

        private void Refresh()
        {
            if (_server != null) { RefreshServer(); return; }
            var config = _data.Shop;
            for (int i = 0; i < _items.Length; i++)
            {
                bool exists = i < config.GoldPacks.Count;
                _items[i].gameObject.SetActive(exists);
                if (!exists) { continue; }

                int index = i;
                var pack = config.GoldPacks[i];
                _items[i].Bind("잉크", _itemIcons != null ? _itemIcons.Coin : null, pack.Gold, ShopRules.GoldPackPrice(config, pack),
                    pack.DiscountPercent, false, !_busy, () => BuyAsync(index).Forget(Debug.LogException),
                    () => _detail?.Show(_itemIcons != null ? _itemIcons.Coin : null, "잉크", $"x{pack.Gold:N0}", ItemInfo.DescriptionOf(RewardRows.CoinId)));
            }
        }

        private async UniTask BuyAsync(int index)
        {
            if (_server != null) { await BuyServerAsync(index); return; }
            if (_busy) { return; }

            var pack = _data.Shop.GoldPacks[index];
            int price = ShopRules.GoldPackPrice(_data.Shop, pack);
            _busy = true;
            Refresh();
            bool confirmed = _confirm == null || await _confirm.ShowAsync(new ConfirmRequest
            {
                Title = "구매",
                Message = $"잉크 x{pack.Gold:N0}을(를)\n{price:N0}잼에 구매할까요?",
                ConfirmLabel = "구매",
                CancelLabel = "취소",
            });
            if (!confirmed)
            {
                _busy = false;
                if (this != null) { Refresh(); }
                return;
            }

            string error = null;
            try
            {
                _profile.Apply(await _api.BuyGoldPack(index, Guid.NewGuid().ToString("N")));
            }
            catch (ApiException rejected)
            {
                error = rejected.Code;
            }
            finally
            {
                _busy = false;
                if (this != null) { Refresh(); }
            }

            if (error == null)
            {
                _rewardPopup?.Show(new List<ResultRewardItem> { new(_itemIcons != null ? _itemIcons.Coin : null, pack.Gold, RewardRows.CoinId) },
                    "잉크를 구매했어요.", "구매 완료");
            }
            else if (error == "INSUFFICIENT_GEMS" && _confirm != null)
            {
                await _confirm.ShowAsync(new ConfirmRequest { Title = "구매", Message = "잼이 부족해요." });
            }
        }

        private async UniTask RefreshServerAsync()
        {
            if (_refreshing || _server.IsBusy) { return; }
            _refreshing = true;
            try
            {
                var restored = await _server.Restore();
                if (this != null && restored?.Status == ShopViewSubmissionStatus.Acknowledged && restored.IsPurchase)
                {
                    var rewards = new List<ResultRewardItem>();
                    foreach (var reward in restored.Rewards)
                    {
                        var icon = reward.ItemId == ShopRules.CoinReward
                            ? _itemIcons != null ? _itemIcons.Coin : null : _itemIcons != null ? _itemIcons.Get(reward.ItemId) : null;
                        rewards.Add(new ResultRewardItem(icon, reward.Count, reward.ItemId));
                    }
                    _rewardPopup?.Show(rewards, "물품을 구매했어요.", "구매 완료");
                }
            }
            catch (ApiException) { }
            finally { _refreshing = false; if (this != null) { Refresh(); } }
        }

        private void RefreshServer()
        {
            if (_profile == null) { return; }
            var catalog = _server.GoldPacks;
            bool ready = _server.Account != null && catalog != null && catalog.Available;
            for (int i = 0; i < _items.Length; i++)
            {
                bool exists = ready && i < catalog.Packs.Count;
                _items[i].gameObject.SetActive(exists);
                if (!exists) { continue; }
                int index = i;
                var pack = catalog.Packs[i];
                _items[i].Bind("잉크", _itemIcons != null ? _itemIcons.Coin : null, pack.Gold,
                    pack.GemPrice, pack.DiscountPercent, false,
                    !_busy && !_server.IsBusy && !_server.HasPending,
                    () => BuyAsync(index).Forget(Debug.LogException),
                    () => _detail?.Show(_itemIcons != null ? _itemIcons.Coin : null, "잉크",
                        $"x{pack.Gold:N0}", ItemInfo.DescriptionOf(RewardRows.CoinId)));
            }
        }

        private async UniTask BuyServerAsync(int index)
        {
            var catalog = _server.GoldPacks;
            if (_busy || _server.IsBusy || _server.HasPending || _server.Account == null
                || catalog == null || index >= catalog.Packs.Count) { return; }
            var pack = catalog.Packs[index];
            _busy = true;
            Refresh();
            bool confirmed;
            try
            {
                confirmed = _confirm == null || await _confirm.ShowAsync(new ConfirmRequest
                {
                    Title = "구매",
                    Message = $"잉크 x{pack.Gold:N0}을(를)\n{pack.GemPrice:N0}잼에 구매할까요?",
                    ConfirmLabel = "구매",
                    CancelLabel = "취소"
                });
            }
            finally { _busy = false; if (this != null) { Refresh(); } }
            if (!confirmed) { return; }
            ShopViewResult result = null;
            _busy = true;
            Refresh();
            try { result = await _server.BuyGoldPack(catalog, index); }
            catch (ApiException) { }
            catch (InvalidOperationException) { await RefreshServerAsync(); }
            finally { _busy = false; if (this != null) { Refresh(); } }

            if (result?.Status == ShopViewSubmissionStatus.Acknowledged)
            {
                _rewardPopup?.Show(new List<ResultRewardItem>
                { new(_itemIcons != null ? _itemIcons.Coin : null, result.Rewards[0].Count, RewardRows.CoinId) },
                    "잉크를 구매했어요.", "구매 완료");
            }
            else if (result?.ErrorCode == "INSUFFICIENT_GEMS" && _confirm != null)
            { await _confirm.ShowAsync(new ConfirmRequest { Title = "구매", Message = "잼이 부족해요." }); }
        }
    }
}
