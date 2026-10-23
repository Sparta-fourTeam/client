using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>로비 오른쪽 메뉴(친구·퀘스트·우편함)가 여는 목록 팝업의 공통 틀. 탭을 고르면 그 탭의 목록을 보여준다.
    /// 아직 서버 기능이 없어 목록은 늘 비어 있고 탭별 안내 문구를 띄운다. 하단 버튼(모두 받기 등)은 기능이 붙기 전까지 비활성이다</summary>
    public abstract class LobbyListPopupView : HudView
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private PopupTransition _transition;
        [SerializeField] private Button _closeButton;

        [Tooltip("탭이 없는 팝업은 비워 둔다")]
        [SerializeField] private Button[] _tabs;
        [SerializeField] private GameObject[] _tabSelected;

        [Tooltip("탭 순서대로, 목록이 비었을 때의 안내. 탭이 없으면 첫 줄만 쓴다")]
        [SerializeField] private string[] _emptyMessages;
        [SerializeField] private TMP_Text _emptyText;

        [Tooltip("아직 없는 기능. 비활성으로 보인다")]
        [SerializeField] private Button[] _disabledButtons;

        public bool IsOpen => _panel.activeSelf;

        public int SelectedTab { get; private set; }

        public string EmptyMessage => _emptyText.text;

        protected override void InitializeView()
        {
            _panel.SetActive(false);
            _closeButton.onClick.AddListener(Close);
            for (int i = 0; i < _tabs.Length; i++)
            {
                int index = i;
                _tabs[i].onClick.AddListener(() => SelectTab(index));
            }

            foreach (var button in _disabledButtons)
            {
                button.interactable = false;
            }
        }

        public void Open()
        {
            PopupPanel.Set(_panel, _transition, true);
            // 다시 열면 첫 탭부터 보여준다
            SelectTab(0);
        }

        public void Close() => PopupPanel.Set(_panel, _transition, false);

        public void SelectTab(int index)
        {
            SelectedTab = index;
            for (int i = 0; i < _tabSelected.Length; i++)
            {
                _tabSelected[i].SetActive(i == index);
            }

            _emptyText.text = index < _emptyMessages.Length ? L10n.Get(_emptyMessages[index]) : "";
            OnTabSelected(index);
        }

        /// <summary>탭이 바뀐 뒤 목록을 실제로 채우는 팝업(퀘스트 등)이 덮어쓴다</summary>
        protected virtual void OnTabSelected(int index) { }

        /// <summary>목록이 비었을 때의 안내 문구 오브젝트. 목록이 있는 탭에서는 끈다</summary>
        protected TMP_Text EmptyLabel => _emptyText;
    }
}
