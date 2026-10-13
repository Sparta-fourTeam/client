using Cysharp.Threading.Tasks;
using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.View
{
    /// <summary>로비 메뉴 버튼으로 여는 중앙 메뉴창. 설정과 안내·계정 버튼, 게임 버전을 표시한다.
    /// 닫기 버튼이나 바깥 배경을 누르면 닫힌다</summary>
    public sealed class LobbyMenuPopupView : HudView, IUiBindable
    {
        public void BindUi(IUiRegistry registry)
        {
            _settings = registry.Get<SettingsPopupView>();
            _confirm = registry.Get<ConfirmPopupView>();
        }

        [Inject]
        public void Construct(AccountService account, PlayerProfile profile)
        {
            _account = account;
            _profile = profile;
        }

        [SerializeField] private GameObject _panel;
        [SerializeField] private PointerClickRelay _background;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Button _closeButton;
        [SerializeField] private TMP_Text _versionText;
        [SerializeField] private PopupTransition _transition;
        [SerializeField] private Button _privacyButton, _termsButton, _supportButton;
        [SerializeField] private Button _logoutButton, _deleteAccountButton;
        [Tooltip("게스트 계정에만 보인다")]
        [SerializeField] private Button _linkAccountButton;

        private SettingsPopupView _settings;
        private ConfirmPopupView _confirm;
        private AccountService _account;
        private PlayerProfile _profile;
        private bool _busy;

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
            _privacyButton.onClick.AddListener(() => Application.OpenURL(ExternalLinks.PrivacyPolicy));
            _termsButton.onClick.AddListener(() => Application.OpenURL(ExternalLinks.TermsOfUse));
            _supportButton.onClick.AddListener(() => Application.OpenURL(ExternalLinks.Support));
            _logoutButton.onClick.AddListener(() => RunAsync(LogoutAsync));
            _deleteAccountButton.onClick.AddListener(() => RunAsync(DeleteAccountAsync));
            _linkAccountButton.onClick.AddListener(() => RunAsync(LinkAccountAsync));
        }

        // 확인창이 떠 있는 동안 같은 흐름을 또 시작하지 않는다
        private void RunAsync(System.Func<UniTask> flow)
        {
            if (_busy)
            {
                return;
            }

            _busy = true;
            Run(flow).Forget(Debug.LogException);
        }

        private async UniTask Run(System.Func<UniTask> flow)
        {
            try { await flow(); }
            finally { _busy = false; }
        }

        private async UniTask LogoutAsync()
        {
            var request = new ConfirmRequest
            {
                Title = "로그아웃",
                Message = "로그아웃하시겠습니까?",
                Warning = _account.IsGuest ? "게스트로 플레이 중이라면 모든 데이터가 삭제되며 복구할 수 없습니다." : null,
                ConfirmLabel = "로그아웃",
                CancelLabel = "취소",
            };
            if (await _confirm.ShowAsync(request))
            {
                await _account.LogoutAsync();
            }
        }

        private async UniTask DeleteAccountAsync()
        {
            var first = new ConfirmRequest
            {
                Title = "계정을 삭제하시겠습니까?",
                Warning = "계정 삭제는 되돌릴 수 없으며, 그 결과에 대한 책임은 본인에게 있습니다.",
                ConfirmLabel = "계정 삭제",
                CancelLabel = "취소",
            };
            if (!await _confirm.ShowAsync(first))
            {
                return;
            }

            var second = new ConfirmRequest
            {
                Title = "계정 삭제",
                Message = "계정 삭제는 되돌릴 수 없으며 그 결과에 대한 책임은 본인에게 있습니다.",
                ConfirmLabel = "계정 삭제",
                CancelLabel = "취소",
                InputPlaceholder = "닉네임을 입력하세요",
                RequiredInput = _profile.Nickname,
            };
            if (await _confirm.ShowAsync(second))
            {
                await _account.DeleteAccountAsync();
            }
        }

        private async UniTask LinkAccountAsync()
        {
            string notice = null;
            switch (await _account.LinkAsync())
            {
                case LinkResult.Linked: notice = "계정이 연동되었습니다."; break;
                case LinkResult.AlreadyLinked: notice = "이미 연동된 계정입니다."; break;
                case LinkResult.Unavailable: notice = "연동할 수 있는 계정이 아직 없습니다. 준비 중입니다."; break;
            }

            RefreshAccountButtons();
            if (notice != null)
            {
                await _confirm.ShowAsync(new ConfirmRequest { Title = "알림", Message = notice });
            }
        }

        private void RefreshAccountButtons() => _linkAccountButton.gameObject.SetActive(_account.IsGuest);

        public void Open()
        {
            _versionText.text = $"Version {Application.version}";
            RefreshAccountButtons();
            PopupPanel.Set(_panel, _transition, true);
        }

        public void Close() => PopupPanel.Set(_panel, _transition, false);

        public void Toggle()
        {
            if (IsOpen) { Close(); } else { Open(); }
        }
    }
}
