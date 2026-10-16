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
            if (_profile != null) { Refresh(); }
        }

        private void Refresh()
        {
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
    }
}
