using UnityEngine;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>재화 칸(코인·잼)을 누르면 상점 화면의 해당 탭을 연다. 칸 아래쪽의 + 표시가 이 버튼이 있다는 것을 알려 준다</summary>
    public sealed class CurrencyShopLink : MonoBehaviour, IUiBindable
    {
        [SerializeField] private Button _button;
        [SerializeField] private ShopTab _tab;

        private LobbyNavView _nav;

        public void BindUi(IUiRegistry registry) => _nav = registry.Get<LobbyNavView>();

        private void Awake() => _button.onClick.AddListener(() => _nav?.OpenShop(_tab));
    }
}
