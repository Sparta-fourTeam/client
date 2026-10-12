using UnityEngine;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>로비 오른쪽 위 메뉴 버튼을 누르면 버튼 아래에 뜨는 메뉴. 설정·계정 연동·계정 삭제·로그아웃이 있고 지금은 설정만 누를 수 있다.
    /// 바깥(배경)을 누르면 닫힌다</summary>
    public sealed class LobbyMenuPopupView : HudView, IUiBindable
    {
        public void BindUi(IUiRegistry registry)
        {
            _settings = registry.Get<SettingsPopupView>();
        }

        [SerializeField] private GameObject _panel;
        [SerializeField] private PointerClickRelay _background;
        [SerializeField] private Button _settingsButton;
        [Tooltip("아직 없는 기능. 비활성으로 보인다")]
        [SerializeField] private Button[] _disabledButtons;

        private SettingsPopupView _settings;

        public bool IsOpen => _panel.activeSelf;

        protected override void InitializeView()
        {
            _panel.SetActive(false);
            _background.Clicked += Close;
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

        public void Open() => _panel.SetActive(true);

        public void Close() => _panel.SetActive(false);

        public void Toggle()
        {
            if (IsOpen) { Close(); } else { Open(); }
        }
    }
}
