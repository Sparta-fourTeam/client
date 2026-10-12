using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>로비 메뉴 버튼으로 여는 중앙 메뉴창. 설정과 안내·계정 버튼, 게임 버전을 표시한다.
    /// 닫기 버튼이나 바깥 배경을 누르면 닫힌다</summary>
    public sealed class LobbyMenuPopupView : HudView, IUiBindable
    {
        public void BindUi(IUiRegistry registry)
        {
            _settings = registry.Get<SettingsPopupView>();
        }

        [SerializeField] private GameObject _panel;
        [SerializeField] private PointerClickRelay _background;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Button _closeButton;
        [SerializeField] private TMP_Text _versionText;
        [SerializeField] private PopupTransition _transition;
        [Tooltip("아직 없는 기능. 비활성으로 보인다")]
        [SerializeField] private Button[] _disabledButtons;

        private SettingsPopupView _settings;

        public bool IsOpen => _transition != null ? _transition.IsShown : _panel.activeSelf;

        protected override void InitializeView()
        {
            _panel.SetActive(false);
            _background.Clicked += Close;
            _closeButton.onClick.AddListener(Close);
            _settingsButton.onClick.AddListener(() =>
            {
                Close();
                _settings.Open();
            });
            foreach (var button in _disabledButtons)
            {
                button.interactable = false;
            }
        }

        public void Open()
        {
            _versionText.text = $"Version {Application.version}";
            PopupPanel.Set(_panel, _transition, true);
        }

        public void Close() => PopupPanel.Set(_panel, _transition, false);

        public void Toggle()
        {
            if (IsOpen) { Close(); } else { Open(); }
        }
    }
}
