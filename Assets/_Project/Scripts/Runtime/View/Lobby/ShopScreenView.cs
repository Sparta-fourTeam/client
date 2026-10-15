using UnityEngine;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>상점 화면의 탭. 숫자는 화면의 탭 순서와 같다</summary>
    public enum ShopTab
    {
        Daily,
        Gacha,
        Gem,
        Gold
    }

    /// <summary>상점 화면(상점과 뽑기를 합친 탭). 하단 메뉴의 상점 탭이 연다. 왼쪽 세로 탭(일일·뽑기·잼·골드)으로 페이지를 고른다</summary>
    public sealed class ShopScreenView : HudView
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private Button[] _tabs;
        [SerializeField] private GameObject[] _tabSelected;
        [SerializeField] private GameObject[] _pages;

        protected override void InitializeView()
        {
            for (int i = 0; i < _tabs.Length; i++)
            {
                int index = i;
                _tabs[i].onClick.AddListener(() => SelectTab((ShopTab)index));
            }
            SelectTab(ShopTab.Daily);
            _panel.SetActive(false);
        }

        public void SetVisible(bool visible) => _panel.SetActive(visible);

        /// <summary>해당 탭의 페이지를 보여준다 (다른 화면의 재화 칸이 상점의 코인·잼 탭을 바로 열 때도 쓴다)</summary>
        public void SelectTab(ShopTab tab)
        {
            int index = (int)tab;
            for (int i = 0; i < _tabSelected.Length; i++) { _tabSelected[i].SetActive(i == index); }
            for (int i = 0; i < _pages.Length; i++) { _pages[i].SetActive(i == index); }
        }
    }
}
