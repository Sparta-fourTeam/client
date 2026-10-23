using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.View
{
    /// <summary>
    /// 상점 화면의 일반 탭(일일상점). 자정(00시)에 물품이 바뀌고, 잉크로 다시 뽑을 수 있다.
    /// 물품 구성은 날짜와 다시 뽑은 횟수로 정해지고(ShopRules.Roll), 다시 뽑기·구매와 재화 소모는 IShopApi가 맡는다.
    /// 구매한 물품은 서버가 보상으로 지급하고, 화면은 받은 것을 보상 팝업으로 보여준다
    /// </summary>
    public sealed class DailyShopView : MonoBehaviour, IUiBindable
    {
        [SerializeField] private TMP_Text _timer;
        [SerializeField] private DailyShopItemView[] _items;
        [SerializeField] private Button _adReroll;
        [SerializeField] private TMP_Text _adCount;
        [SerializeField] private Button _goldReroll;
        [SerializeField] private TMP_Text _goldCost;
        [SerializeField] private TMP_Text _goldCount;

        [Header("보상 표시")]
        [SerializeField] private ItemIconTable _itemIcons;
        [SerializeField] private Sprite _energyIcon;

        private PlayerProfile _profile;
        private GameDataStore _data;
        private IShopApi _api;
        private IPlayerApi _player;
        private RewardPopupView _rewardPopup;
        private DetailPopupView _detail;
        private ConfirmPopupView _confirm;
        private List<ShopOffer> _offers;
        private string _rolledKey;
        private string _dayKey;
        private int _shownMinute = -1;
        private bool _busy;
        private bool _refreshingDay;
        private float _nextServerDayCheck;
        private IServerShopViewSession _server;

        /// <summary>서버 모드를 명시적으로 주입한다. 현재 Root의 Local 등록은 변경하지 않는다.</summary>
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
        public void Construct(PlayerProfile profile, GameDataStore data, IShopApi api, IPlayerApi player)
        {
            _profile = profile;
            _data = data;
            _api = api;
            _player = player;
            _dayKey = ShopRules.DayKey(DateTime.UtcNow);
            if (isActiveAndEnabled)
            {
                _profile.Changed += OnProfileChanged;
                Refresh();
            }
        }

        private void Awake()
        {
            _adReroll.interactable = false;
            _goldReroll.onClick.AddListener(() => RerollAsync().Forget(Debug.LogException));
        }

        private void OnEnable()
        {
            // 주입 전에 켜지면 Construct가 이어서 그린다
            if (_profile == null) { return; }
            // 구독은 켜져 있는 동안만 둔다. 비활성인 채 씬이 내려가면 OnDestroy가 불리지 않아 파괴된 뷰에 구독이 남는다
            _profile.Changed -= OnProfileChanged;
            _profile.Changed += OnProfileChanged;
            if (_server != null) { _server.Changed += Refresh; }
            _dayKey = ShopRules.DayKey(DateTime.UtcNow);
            _shownMinute = -1;
            Refresh();
            UpdateTimer();
            if (_server != null) { RefreshServerAsync().Forget(Debug.LogException); }
        }

        private void OnDisable()
        {
            if (_profile != null) { _profile.Changed -= OnProfileChanged; }
            if (_server != null) { _server.Changed -= Refresh; }
        }

        private void Update()
        {
            if (_profile == null) { return; }
            if (_server != null)
            {
                if (Time.unscaledTime >= _nextServerDayCheck
                    && (_server.HasPending || _server.Account == null || _server.Account.Daily == null
                        || DateTimeOffset.UtcNow >= _server.Account.Daily.NextResetAt))
                {
                    _nextServerDayCheck = Time.unscaledTime + 10f;
                    RefreshServerAsync().Forget(Debug.LogException);
                }
                UpdateTimer();
                return;
            }
            if (ShopRules.DayKey(DateTime.UtcNow) != _dayKey) { RefreshDayAsync().Forget(Debug.LogException); }
            UpdateTimer();
        }

        private void OnProfileChanged(PlayerProfile profile) => Refresh();

        // 화면을 켜 둔 채 00시가 지나면 서버가 날을 넘긴 상태를 다시 받아 온다
        private async UniTask RefreshDayAsync()
        {
            if (_refreshingDay) { return; }
            _refreshingDay = true;
            try
            {
                _profile.Apply(await _player.GetMe());
                _dayKey = ShopRules.DayKey(DateTime.UtcNow);
            }
            catch (ApiException)
            {
            }
            finally
            {
                _refreshingDay = false;
            }
        }

        private async UniTask RerollAsync()
        {
            if (_server != null) { await RerollServerAsync(); return; }
            if (_busy) { return; }

            var config = _data.Shop;
            var state = _profile.Shop;
            int left = ShopRules.GoldLeft(config, state);
            string question = L10n.Get("dailyShop.resetQuestion", config.GoldRerollCost.ToString("N0"));
            string remainingText = L10n.Get("common.remainingCount", $"{left}/{config.GoldRerollsPerDay}");

            // 골드가 모자라도 누를 수 있다. 서버가 거절하면 그때 모자라다고 알려 준다
            _busy = true;
            Refresh();
            bool confirmed = _confirm == null || await _confirm.ShowAsync(new ConfirmRequest
            {
                Title = L10n.Get("shop.dailyReset"),
                Message = question + "\n" + remainingText + "\n" + L10n.Get("dailyShop.resetNote"),
                ConfirmLabel = L10n.Get("shop.reset"),
                CancelLabel = L10n.Get("common.cancel"),
            });
            _busy = false;
            if (!confirmed)
            {
                if (this != null) { Refresh(); }
                return;
            }

            string error = await RunAsync(() => _api.Reroll(ShopRerollKind.Gold, Guid.NewGuid().ToString("N")));
            if (error == "INSUFFICIENT_GOLD" && _confirm != null)
            {
                await _confirm.ShowAsync(new ConfirmRequest { Title = L10n.Get("shop.dailyReset"), Message = L10n.Get("dailyShop.notEnoughInk") });
            }
        }

        private async UniTask BuyAsync(int slot)
        {
            if (_server != null) { await BuyServerAsync(slot); return; }
            if (_busy || _offers == null || slot >= _offers.Count) { return; }

            var offer = _offers[slot];
            string price = offer.Price > 0 ? L10n.Get("shop.gemPrice", offer.Price.ToString("N0")) : L10n.Get("shop.free");
            // 잼이 모자라도 누를 수 있다. 서버가 거절하면 그때 모자라다고 알려 준다. 무료 물품은 묻지 않고 바로 받는다
            _busy = true;
            Refresh();
            bool confirmed = offer.Price <= 0 || _confirm == null || await _confirm.ShowAsync(new ConfirmRequest
            {
                Title = L10n.Get("shop.buy"),
                Message = L10n.Get("shop.buyConfirm", $"{NameOf(offer.Reward)} x{offer.Count:N0}", price),
                ConfirmLabel = L10n.Get("shop.buy"),
                CancelLabel = L10n.Get("common.cancel"),
            });
            _busy = false;
            if (!confirmed)
            {
                if (this != null) { Refresh(); }
                return;
            }

            string error = await RunAsync(() => _api.Buy(slot, Guid.NewGuid().ToString("N")));
            if (error == null)
            {
                _rewardPopup?.Show(new List<ResultRewardItem> { new(IconOf(offer.Reward), offer.Count, offer.Reward) },
                    L10n.Get("shop.purchased"), L10n.Get("shop.purchaseDone"));
            }
            else if (error == "INSUFFICIENT_GEMS" && _confirm != null)
            {
                await _confirm.ShowAsync(new ConfirmRequest { Title = L10n.Get("shop.buy"), Message = L10n.Get("shop.notEnoughGems") });
            }
        }

        /// <summary>요청 하나를 보내고 응답을 프로필에 반영한다. 거절되면 서버 기준 상태로 다시 그리고 거절 코드를 돌려준다. 성공하면 null</summary>
        private async UniTask<string> RunAsync(Func<UniTask<PlayerSnapshot>> request)
        {
            if (_busy) { return "BUSY"; }

            _busy = true;
            Refresh();
            try
            {
                _profile.Apply(await request());
                return null;
            }
            catch (ApiException error)
            {
                return error.Code;
            }
            finally
            {
                _busy = false;
                if (this != null) { Refresh(); }
            }
        }

        private string NameOf(string reward) => reward switch
        {
            ShopRules.CoinReward => L10n.Get("currency.ink"),
            ShopRules.EnergyReward => L10n.Get("currency.inkPen"),
            _ => ItemInfo.NameOf(reward, _data),
        };

        private Sprite IconOf(string reward) => reward switch
        {
            ShopRules.CoinReward => _itemIcons != null ? _itemIcons.Coin : null,
            ShopRules.EnergyReward => _energyIcon,
            _ => _itemIcons != null ? _itemIcons.Get(reward) : null,
        };

        private void Refresh()
        {
            if (_server != null) { RefreshServer(); return; }
            var state = _profile.Shop;
            string dayKey = ShopRules.DayKey(DateTime.UtcNow);
            int rerolls = state.adUsed + state.goldUsed;
            string rolled = $"{dayKey}:{rerolls}";
            if (_offers == null || rolled != _rolledKey)
            {
                _rolledKey = rolled;
                _offers = ShopRules.Roll(_data.Shop, dayKey, rerolls);
            }

            for (int i = 0; i < _items.Length && i < _offers.Count; i++)
            {
                int slot = i;
                var offer = _offers[i];
                bool canBuy = !_busy;
                _items[i].Bind(NameOf(offer.Reward), IconOf(offer.Reward), offer.Count, offer.Price, offer.DiscountPercent,
                    _profile.IsShopSlotSold(i), canBuy, () => BuyAsync(slot).Forget(Debug.LogException),
                    () => _detail?.Show(IconOf(offer.Reward), NameOf(offer.Reward), $"x{offer.Count:N0}", ItemInfo.DescriptionOf(offer.Reward)));
            }

            var config = _data.Shop;
            int goldLeft = ShopRules.GoldLeft(config, state);
            _adCount.text = "0/0";
            _goldCount.text = $"{goldLeft}/{config.GoldRerollsPerDay}";
            _goldCost.text = config.GoldRerollCost.ToString("N0");
            _adReroll.interactable = false;
            _goldReroll.interactable = !_busy && goldLeft > 0;
        }

        private void UpdateTimer()
        {
            var utcNow = DateTime.UtcNow;
            var remain = _server?.Account?.Daily != null
                ? _server.Account.Daily.NextResetAt.UtcDateTime - utcNow : GameDay.NextResetUtc(utcNow) - utcNow;
            if (_server != null && _server.Account?.Daily == null)
            {
                _timer.text = L10n.Get("common.checking");
                return;
            }
            if (remain < TimeSpan.Zero) { remain = TimeSpan.Zero; }
            int minute = (int)remain.TotalMinutes;
            if (minute == _shownMinute) { return; }
            _shownMinute = minute;
            _timer.text = L10n.Get("dailyShop.timeLeft", minute / 60, (minute % 60).ToString("D2"));
        }

        private async UniTask RefreshServerAsync()
        {
            if (_refreshingDay || _server.IsBusy) { return; }
            _refreshingDay = true;
            try
            {
                var restored = await _server.Restore();
                if (this != null && restored?.Status == ShopViewSubmissionStatus.Acknowledged && restored.IsPurchase)
                {
                    _rewardPopup?.Show(restored.Rewards.Select(reward =>
                        new ResultRewardItem(IconOf(reward.ItemId), reward.Count, reward.ItemId)).ToList(),
                        L10n.Get("shop.purchased"), L10n.Get("shop.purchaseDone"));
                }
            }
            catch (ApiException) { }
            finally { _refreshingDay = false; if (this != null) { Refresh(); UpdateTimer(); } }
        }

        private void RefreshServer()
        {
            if (_profile == null) { return; }
            var daily = _server.Account?.Daily;
            bool ready = daily != null && daily.Available;
            for (int i = 0; i < _items.Length; i++)
            {
                bool exists = ready && i < daily.Offers.Count;
                _items[i].gameObject.SetActive(exists);
                if (!exists) { continue; }
                int slot = i;
                var offer = daily.Offers[i];
                var rewards = offer.Rewards;
                string name = rewards.Count == 1 ? NameOf(rewards[0].ItemId)
                    : string.Join(" · ", rewards.Select(reward => $"{NameOf(reward.ItemId)} x{reward.Count:N0}"));
                string count = rewards.Count == 1 ? $"x{rewards[0].Count:N0}" : string.Empty;
                Sprite icon = rewards.Count > 0 ? IconOf(rewards[0].ItemId) : null;
                bool sold = daily.Purchased.Contains(slot);
                _items[i].BindServer(name, icon, count, offer.Price, offer.DiscountPercent,
                    sold, !_busy && !_server.IsBusy && !_server.HasPending, () => BuyAsync(slot).Forget(Debug.LogException),
                    () => _detail?.Show(icon, name, count, string.Join("\n", rewards.Select(reward =>
                        $"{NameOf(reward.ItemId)} x{reward.Count:N0}"))));
            }
            _adCount.text = "0/0";
            _adReroll.interactable = false;
            _goldCount.text = _server.HasPending ? L10n.Get("common.checking") : ready
                ? $"{daily.GoldLeft}/{daily.GoldLeft + daily.GoldUsed}" : "0/0";
            _goldCost.text = ready ? daily.GoldRerollCost.ToString("N0") : "0";
            _goldReroll.interactable = ready && daily.GoldLeft > 0 && !_busy && !_server.IsBusy && !_server.HasPending;
            _shownMinute = -1;
            UpdateTimer();
        }

        private async UniTask RerollServerAsync()
        {
            var quoted = _server.Account?.Daily;
            if (_busy || _server.IsBusy || _server.HasPending || quoted == null || quoted.GoldLeft == 0) { return; }
            _busy = true;
            Refresh();
            bool confirmed;
            try
            {
                confirmed = _confirm == null || await _confirm.ShowAsync(new ConfirmRequest
                {
                    Title = L10n.Get("shop.dailyReset"),
                    Message = L10n.Get("dailyShop.resetQuestion", quoted.GoldRerollCost.ToString("N0")) + "\n" + L10n.Get("common.remainingCount", quoted.GoldLeft),
                    ConfirmLabel = L10n.Get("shop.reset"),
                    CancelLabel = L10n.Get("common.cancel")
                });
            }
            finally { _busy = false; if (this != null) { Refresh(); } }
            if (!confirmed) { return; }
            var result = await RunServerAsync(() => _server.GoldReroll(quoted));
            if (result?.ErrorCode == "INSUFFICIENT_GOLD" && _confirm != null)
            { await _confirm.ShowAsync(new ConfirmRequest { Title = L10n.Get("shop.dailyReset"), Message = L10n.Get("dailyShop.notEnoughInk") }); }
        }

        private async UniTask BuyServerAsync(int slot)
        {
            var daily = _server.Account?.Daily;
            if (_busy || _server.IsBusy || _server.HasPending || daily == null || slot >= daily.Offers.Count
                || daily.Purchased.Contains(slot)) { return; }
            var offer = daily.Offers[slot];
            var rewards = offer.Rewards;
            string name = string.Join(" · ", rewards.Select(reward => $"{NameOf(reward.ItemId)} x{reward.Count:N0}"));
            _busy = true;
            Refresh();
            bool confirmed;
            try
            {
                confirmed = offer.Price <= 0 || _confirm == null || await _confirm.ShowAsync(new ConfirmRequest
                {
                    Title = L10n.Get("shop.buy"),
                    Message = L10n.Get("shop.buyConfirm", name, L10n.Get("shop.gemPrice", offer.Price.ToString("N0"))),
                    ConfirmLabel = L10n.Get("shop.buy"),
                    CancelLabel = L10n.Get("common.cancel")
                });
            }
            finally { _busy = false; if (this != null) { Refresh(); } }
            if (!confirmed) { return; }
            var result = await RunServerAsync(() => _server.BuyDaily(daily, slot));
            if (result?.Status == ShopViewSubmissionStatus.Acknowledged)
            {
                var items = result.Rewards.Select(reward =>
                    new ResultRewardItem(IconOf(reward.ItemId), reward.Count, reward.ItemId)).ToList();
                _rewardPopup?.Show(items, L10n.Get("shop.purchased"), L10n.Get("shop.purchaseDone"));
            }
            else if (result?.ErrorCode == "INSUFFICIENT_GEMS" && _confirm != null)
            { await _confirm.ShowAsync(new ConfirmRequest { Title = L10n.Get("shop.buy"), Message = L10n.Get("shop.notEnoughGems") }); }
        }

        private async UniTask<ShopViewResult> RunServerAsync(Func<UniTask<ShopViewResult>> request)
        {
            _busy = true;
            Refresh();
            try { return await request(); }
            catch (ApiException) { return null; }
            catch (InvalidOperationException) { await RefreshServerAsync(); return null; }
            finally { _busy = false; if (this != null) { Refresh(); } }
        }
    }
}
