using System;
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
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _count;
        [SerializeField] private GameObject _discountBadge;
        [SerializeField] private TMP_Text _discountLabel;
        [SerializeField] private Button _buy;
        [SerializeField] private GameObject _gemIcon;
        [SerializeField] private TMP_Text _price;
        [SerializeField] private GameObject _soldOut;

        public void Bind(string itemName, Sprite icon, int count, int price, int discountPercent, bool soldOut, bool canBuy, Action onBuy)
        {
            _name.text = itemName;
            _icon.sprite = icon;
            _icon.enabled = icon != null;
            _count.text = $"x{count:N0}";
            _discountBadge.SetActive(discountPercent > 0);
            _discountLabel.text = $"{discountPercent}%\n할인";
            _gemIcon.SetActive(price > 0);
            _price.text = price > 0 ? price.ToString("N0") : "무료";
            _soldOut.SetActive(soldOut);
            _buy.interactable = !soldOut && canBuy;
            _buy.onClick.RemoveAllListeners();
            _buy.onClick.AddListener(() => onBuy());
        }
    }
}
