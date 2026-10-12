using UnityEngine;

namespace Game.View
{
    /// <summary>상점 화면(상점과 뽑기를 합친 탭). 하단 메뉴의 상점 탭이 연다. 아직 내용은 없고 패널만 띄운다</summary>
    public sealed class ShopScreenView : HudView
    {
        [SerializeField] private GameObject _panel;

        protected override void InitializeView()
        {
            _panel.SetActive(false);
        }

        public void SetVisible(bool visible) => _panel.SetActive(visible);
    }
}
