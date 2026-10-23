using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Core;
using UnityEngine;
using VContainer;

namespace Game.View
{
    /// <summary>
    /// 상점 화면의 잼 탭. 현금으로 잼 묶음을 산다(ShopConfig.GemPacks). 큰 묶음일수록 보너스가 붙는다.
    /// 실제 결제는 아직 없고 확인하면 바로 잼을 준다(IShopApi.BuyGemPack의 TODO(server))
    /// </summary>
    public sealed class GemShopView : MonoBehaviour, IUiBindable
    {
        [SerializeField] private DailyShopItemView[] _items;
        [Tooltip("보상 팝업에 보여 줄 잼 아이콘")]
        [SerializeField] private Sprite _gemIcon;

        private PlayerProfile _profile;
        private GameDataStore _data;
        private IShopApi _api;
        private RewardPopupView _rewardPopup;
        private DetailPopupView _detail;
        private ConfirmPopupView _confirm;
        private ComingSoonToastView _toast;
        private bool _busy;

        public void BindUi(IUiRegistry registry)
        {
            _toast = registry.Get<ComingSoonToastView>();
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

        private static string PriceLabel(GemPackDefinition pack) => $"₩{pack.PriceKrw:N0}";

        private void Refresh()
        {
            var packs = _data.Shop.GemPacks;
            for (int i = 0; i < _items.Length; i++)
            {
                bool exists = i < packs.Count;
                _items[i].gameObject.SetActive(exists);
                if (!exists) { continue; }

                int index = i;
                var pack = packs[i];
                _items[i].BindCash(() => L10n.Get("currency.gem"), _gemIcon, pack.Gems, PriceLabel(pack), pack.BonusPercent, !_busy, () => BuyAsync(index).Forget(Debug.LogException),
                    () => _detail?.Show(_gemIcon, L10n.Get("currency.gem"), $"x{pack.Gems:N0}", ItemInfo.DescriptionOf(RewardRows.GemId)));
            }
        }

        private async UniTask BuyAsync(int index)
        {
            if (_busy) { return; }

            var pack = _data.Shop.GemPacks[index];
            _busy = true;
            Refresh();
            bool confirmed = _confirm == null || await _confirm.ShowAsync(new ConfirmRequest
            {
                Title = L10n.Get("shop.buy"),
                Message = L10n.Get("shop.buyConfirm", $"{L10n.Get("currency.gem")} x{pack.Gems:N0}", PriceLabel(pack)),
                ConfirmLabel = L10n.Get("shop.buy"),
                CancelLabel = L10n.Get("common.cancel"),
            });
            if (!confirmed)
            {
                _busy = false;
                if (this != null) { Refresh(); }
                return;
            }

            bool bought = false;
            try
            {
                _profile.Apply(await _api.BuyGemPack(index, Guid.NewGuid().ToString("N")));
                bought = true;
            }
            catch (ApiException rejected)
            {
                Debug.LogWarning($"잼 묶음 구매가 거절됐다: {rejected.Code}");
                // 서버 진행에서는 결제가 아직 없어 구매가 항상 거절된다. 눌러도 아무 반응이 없어 보이지 않게 알린다
                _toast?.Show(L10n.Get("gemShop.notReady"));
            }
            finally
            {
                _busy = false;
                if (this != null) { Refresh(); }
            }

            if (bought)
            {
                _rewardPopup?.Show(new List<ResultRewardItem> { new(_gemIcon, pack.Gems, RewardRows.GemId) }, L10n.Get("shop.gemPurchased"), L10n.Get("shop.purchaseDone"));
            }
        }
    }
}
