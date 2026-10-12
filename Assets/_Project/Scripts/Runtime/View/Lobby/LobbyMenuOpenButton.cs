using UnityEngine;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>로비 오른쪽 위 메뉴 버튼. 누르면 메뉴를 열고, 열려 있으면 닫는다</summary>
    [RequireComponent(typeof(Button))]
    public sealed class LobbyMenuOpenButton : MonoBehaviour, IUiBindable
    {
        private LobbyMenuPopupView _menu;
        public void BindUi(IUiRegistry registry) => _menu = registry.Get<LobbyMenuPopupView>();
        private void Awake() => GetComponent<Button>().onClick.AddListener(() => _menu.Toggle());
    }
}
