using System;
using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>일일상점 물품 카드. 보이는 것만 맡고 구매·갱신 규칙은 DailyShopView가 정한다.
    /// 가격이 0이면 무료, 품절이면 어둡게 덮고 구매 버튼을 막는다</summary>
    public sealed class DailyShopItemView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _name;
        [Tooltip("공용 아이템 칸(ItemCell). 아이콘과 개수를 그리고, 누르면 아이템 정보가 열린다")]
        [SerializeField] private ItemCellView _cell;
        [SerializeField] private GameObject _discountBadge;
        [SerializeField] private TMP_Text _discountLabel;
        [SerializeField] private Button _buy;
        [SerializeField] private GameObject _gemIcon;
        [SerializeField] private TMP_Text _price;
        [SerializeField] private GameObject _soldOut;
        [Tooltip("받을 수 있는 무료 물품에 켜지는 알림 점")]
        [SerializeField] private GameObject _freeBadge;

        /// <summary>현금으로 사는 묶음 카드(잼 상점). 가격은 이미 만든 문구로 받고 잼 아이콘은 숨기며, 보너스가 있으면 배지에 보여 준다</summary>
        public void BindCash(Func<string> itemName, Sprite icon, int count, string priceLabel, int bonusPercent, bool canBuy, Action onBuy, Action onInfo = null)
        {
            Bind(itemName, icon, count, 0, 0, false, canBuy, onBuy, onInfo);
            _discountBadge.SetActive(bonusPercent > 0);
            _discountLabel.SetL10n("dailyShop.bonusBadge", bonusPercent);
            _gemIcon.SetActive(false);
            _price.text = priceLabel;
            SetFreeBadge(false);
        }

        public void Bind(Func<string> itemName, Sprite icon, int count, int price, int discountPercent, bool soldOut, bool canBuy, Action onBuy, Action onInfo = null)
            => BindDisplay(itemName, icon, $"x{count:N0}", price, discountPercent, soldOut, canBuy, onBuy, onInfo);

        /// <summary>서버가 확정한 복합 보상은 숫자 하나로 축약하지 않고 이름·수량을 모두 표시한다.</summary>
        public void BindServer(Func<string> itemName, Sprite icon, string countLabel, int price, int discountPercent,
            bool soldOut, bool canBuy, Action onBuy, Action onInfo = null)
            => BindDisplay(itemName, icon, countLabel, price, discountPercent, soldOut, canBuy, onBuy, onInfo);

        private void BindDisplay(Func<string> itemName, Sprite icon, string countLabel, int price, int discountPercent,
            bool soldOut, bool canBuy, Action onBuy, Action onInfo)
        {
            _name.SetL10n(itemName);
            _cell.Show(icon, countLabel, onInfo);
            _discountBadge.SetActive(discountPercent > 0);
            _discountLabel.SetL10n("dailyShop.discountBadge", discountPercent);
            _gemIcon.SetActive(price > 0);
            if (price > 0) { _price.text = price.ToString("N0"); }
            else { _price.SetL10n("shop.free"); }
            _soldOut.SetActive(soldOut);
            SetFreeBadge(price <= 0 && !soldOut);
            _buy.interactable = !soldOut && canBuy;
            _buy.onClick.RemoveAllListeners();
            _buy.onClick.AddListener(() => onBuy());
        }

        private void SetFreeBadge(bool visible)
        {
            if (_freeBadge != null) { _freeBadge.SetActive(visible); }
        }
    }
}
