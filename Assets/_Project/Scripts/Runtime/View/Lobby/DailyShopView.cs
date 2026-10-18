using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.View
{
    /// <summary>
    /// 상점 화면의 일반 탭(일일상점). 자정(00시)에 물품이 바뀌고, 광고나 코인으로 다시 뽑을 수 있다.
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
            _adReroll.onClick.AddListener(() => RerollAsync(ShopRerollKind.Ad).Forget(Debug.LogException));
            _goldReroll.onClick.AddListener(() => RerollAsync(ShopRerollKind.Gold).Forget(Debug.LogException));
        }

        private void OnEnable()
        {
            // 주입 전에 켜지면 Construct가 이어서 그린다
            if (_profile == null) { return; }
            // 구독은 켜져 있는 동안만 둔다. 비활성인 채 씬이 내려가면 OnDestroy가 불리지 않아 파괴된 뷰에 구독이 남는다
            _profile.Changed -= OnProfileChanged;
            _profile.Changed += OnProfileChanged;
            _dayKey = ShopRules.DayKey(DateTime.UtcNow);
            _shownMinute = -1;
            Refresh();
            UpdateTimer();
        }

        private void OnDisable()
        {
            if (_profile != null) { _profile.Changed -= OnProfileChanged; }
        }

        private void Update()
        {
            if (_profile == null) { return; }
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

        private async UniTask RerollAsync(ShopRerollKind kind)
        {
            if (_busy) { return; }

            var config = _data.Shop;
            var state = _profile.Shop;
            bool ad = kind == ShopRerollKind.Ad;
            int left = ad ? ShopRules.AdLeft(config, state) : ShopRules.GoldLeft(config, state);
            int max = ad ? config.AdRerollsPerDay : config.GoldRerollsPerDay;
            string question = ad
                ? "광고를 보고 상점을 초기화할까요?"
                : $"{config.GoldRerollCost:N0} 잉크를 사용해 상점을 초기화할까요?";

            // 골드가 모자라도 누를 수 있다. 서버가 거절하면 그때 모자라다고 알려 준다
            _busy = true;
            Refresh();
            bool confirmed = _confirm == null || await _confirm.ShowAsync(new ConfirmRequest
            {
                Title = "일일상점 초기화",
                Message = $"{question}\n남은 횟수 {left}/{max}\n초기화하면 품절된 상품도 새로 채워져요.",
                ConfirmLabel = ad ? "광고 보기" : "초기화",
                CancelLabel = "취소",
            });
            _busy = false;
            if (!confirmed)
            {
                if (this != null) { Refresh(); }
                return;
            }

            string error = await RunAsync(() => _api.Reroll(kind, Guid.NewGuid().ToString("N")));
            if (error == "INSUFFICIENT_GOLD" && _confirm != null)
            {
                await _confirm.ShowAsync(new ConfirmRequest { Title = "일일상점 초기화", Message = "잉크가 부족해요." });
            }
        }

        private async UniTask BuyAsync(int slot)
        {
            if (_busy || _offers == null || slot >= _offers.Count) { return; }

            var offer = _offers[slot];
            string price = offer.Price > 0 ? $"{offer.Price:N0}잼" : "무료";
            // 잼이 모자라도 누를 수 있다. 서버가 거절하면 그때 모자라다고 알려 준다. 무료 물품은 묻지 않고 바로 받는다
            _busy = true;
            Refresh();
            bool confirmed = offer.Price <= 0 || _confirm == null || await _confirm.ShowAsync(new ConfirmRequest
            {
                Title = "구매",
                Message = $"{NameOf(offer.Reward)} x{offer.Count:N0}을(를)\n{price}에 구매할까요?",
                ConfirmLabel = "구매",
                CancelLabel = "취소",
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
                    "물품을 구매했어요.", "구매 완료");
            }
            else if (error == "INSUFFICIENT_GEMS" && _confirm != null)
            {
                await _confirm.ShowAsync(new ConfirmRequest { Title = "구매", Message = "잼이 부족해요." });
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
            ShopRules.CoinReward => "잉크",
            ShopRules.EnergyReward => "잉크펜",
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
            int adLeft = ShopRules.AdLeft(config, state);
            int goldLeft = ShopRules.GoldLeft(config, state);
            _adCount.text = $"{adLeft}/{config.AdRerollsPerDay}";
            _goldCount.text = $"{goldLeft}/{config.GoldRerollsPerDay}";
            _goldCost.text = config.GoldRerollCost.ToString("N0");
            _adReroll.interactable = !_busy && adLeft > 0;
            _goldReroll.interactable = !_busy && goldLeft > 0;
        }

        private void UpdateTimer()
        {
            var utcNow = DateTime.UtcNow;
            var remain = GameDay.NextResetUtc(utcNow) - utcNow;
            int minute = (int)remain.TotalMinutes;
            if (minute == _shownMinute) { return; }
            _shownMinute = minute;
            _timer.text = $"{minute / 60}시간 {minute % 60:D2}분";
        }
    }
}
