using UnityEngine;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>상점 화면(상점과 뽑기를 합친 탭). 하단 메뉴의 상점 탭이 연다. 왼쪽 세로 탭으로 페이지를 고르며 아직 내용은 비어 있다</summary>
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
                _tabs[i].onClick.AddListener(() => SelectTab(index));
            }
            SelectTab(0);
            _panel.SetActive(false);
        }

        public void SetVisible(bool visible) => _panel.SetActive(visible);

        private void SelectTab(int index)
        {
            for (int i = 0; i < _tabSelected.Length; i++) { _tabSelected[i].SetActive(i == index); }
            for (int i = 0; i < _pages.Length; i++) { _pages[i].SetActive(i == index); }
        }
    }
}
