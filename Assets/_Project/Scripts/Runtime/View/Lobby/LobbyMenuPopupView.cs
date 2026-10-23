using Cysharp.Threading.Tasks;
using Game.Core;
using MessagePipe;
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
            _noticeList = registry.Get<NoticeListPopupView>();
        }

        [Inject]
        public void Construct(AccountService account, PlayerProfile profile)
        {
            _account = account;
            _profile = profile;
        }

        [Inject]
        public void ConstructNotices(NoticeService notices, IBufferedSubscriber<NoticeChanged> noticeChanged)
        {
            _notices = notices;
            Track(noticeChanged.Subscribe(_ => RefreshNoticeBadge()));
            RefreshNoticeBadge();
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
        [SerializeField] private Button _noticeButton;
        [Tooltip("읽지 않은 공지가 있을 때만 켠다")]
        [SerializeField] private GameObject _noticeBadge;

        private NoticeListPopupView _noticeList;
        private NoticeService _notices;
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
            _noticeButton.onClick.AddListener(() =>
            {
                Close();
                _noticeList.Open();
            });
            BindDocument(_privacyButton, ExternalLinks.PrivacyPolicy);
            BindDocument(_termsButton, ExternalLinks.TermsOfUse);
            _supportButton.onClick.AddListener(() => RunAsync(ContactAsync));
            _logoutButton.onClick.AddListener(() => RunAsync(LogoutAsync));
            _deleteAccountButton.onClick.AddListener(() => RunAsync(DeleteAccountAsync));
            _linkAccountButton.onClick.AddListener(() => RunAsync(LinkAccountAsync));
        }

        private static void BindDocument(Button button, string url)
        {
            bool available = System.Uri.TryCreate(url, System.UriKind.Absolute, out var uri) &&
                uri.Scheme == System.Uri.UriSchemeHttps && uri.Host != "example.com";
            button.interactable = available;
            if (available) { button.onClick.AddListener(() => Application.OpenURL(url)); }
            else { button.GetComponentInChildren<TMP_Text>(true).text += $" ({L10n.Get("common.comingSoon")})"; }
        }

        // 문의 내용을 클립보드에 복사한 뒤 메일 작성 화면을 연다. 메일 앱이 없어 열리지 않아도 붙여 넣어 보낼 수 있다
        private async UniTask ContactAsync()
        {
            string body = SupportMail.Body(_profile.Nickname, _account.IsGuest ? "게스트" : "소셜 계정", null,
                SupportMail.ReadDiagnosticId(), Application.version, SystemInfo.deviceModel, SystemInfo.operatingSystem);
            GUIUtility.systemCopyBuffer = body;
            Application.OpenURL(SupportMail.Uri(body));
            await _confirm.ShowAsync(new ConfirmRequest
            {
                Title = L10n.Get("menu.contact"),
                Message = L10n.Get("menu.contactBody", SupportMail.Address),
            });
        }

        // 확인창이 떠 있는 동안 같은 흐름을 또 시작하지 않는다
        private void RunAsync(System.Func<UniTask> flow)
        {
            if (_busy)
            {
                return;
            }

            SetBusy(true);
            Run(flow).Forget(Debug.LogException);
        }

        private async UniTask Run(System.Func<UniTask> flow)
        {
            try { await flow(); }
            finally { SetBusy(false); }
        }

        // 읽지 않은 공지가 있으면 메뉴의 공지사항 버튼에 빨간 점을 켠다
        private void RefreshNoticeBadge()
        {
            if (_noticeBadge != null && _notices != null)
            {
                _noticeBadge.SetActive(_notices.UnreadCount > 0);
            }
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            if (_deleteAccountButton != null) { _deleteAccountButton.interactable = !busy; }
            if (_logoutButton != null) { _logoutButton.interactable = !busy; }
            if (_linkAccountButton != null) { _linkAccountButton.interactable = !busy; }
            if (_settingsButton != null) { _settingsButton.interactable = !busy; }
            if (_supportButton != null) { _supportButton.interactable = !busy; }
            if (_closeButton != null) { _closeButton.interactable = !busy; }
        }

        private async UniTask LogoutAsync()
        {
            var request = new ConfirmRequest
            {
                Title = L10n.Get("menu.logout"),
                Message = L10n.Get("menu.logoutConfirm"),
                Warning = _account.IsGuest ? L10n.Get("menu.logoutGuestWarning") : null,
                ConfirmLabel = L10n.Get("menu.logout"),
                CancelLabel = L10n.Get("common.cancel"),
            };
            if (await _confirm.ShowAsync(request))
            {
                if (_account.IsGuest) { await DeleteAccountAsync(); }
                else { await ExecuteAccountExitAsync(_account.LogoutAsync, L10n.Get("menu.logout")); }
            }
        }

        private async UniTask DeleteAccountAsync()
        {
            var first = new ConfirmRequest
            {
                Title = L10n.Get("menu.deleteConfirmTitle"),
                Warning = L10n.Get("menu.deleteWarning"),
                ConfirmLabel = L10n.Get("menu.deleteAccount"),
                CancelLabel = L10n.Get("common.cancel"),
            };
            if (!await _confirm.ShowAsync(first))
            {
                return;
            }

            var second = new ConfirmRequest
            {
                Title = L10n.Get("menu.deleteAccount"),
                Message = L10n.Get("menu.deleteTypeNickname", _profile.Nickname),
                ConfirmLabel = L10n.Get("menu.deleteAccount"),
                CancelLabel = L10n.Get("common.cancel"),
                InputPlaceholder = L10n.Get("menu.nicknamePlaceholder"),
                RequiredInput = _profile.Nickname,
            };
            if (await _confirm.ShowAsync(second))
            {
                await ExecuteAccountExitAsync(_account.DeleteAccountAsync, L10n.Get("menu.deleteAccount"),
                    _deleteAccountButton.GetComponentInChildren<TMP_Text>(true));
            }
        }

        private async UniTask ExecuteAccountExitAsync(System.Func<UniTask> exit, string operation, TMP_Text progressLabel = null)
        {
            string original = progressLabel != null ? progressLabel.text : null;
            if (progressLabel != null) { progressLabel.text = L10n.Get("menu.deleting"); }
            string failure = null;
            bool reauthenticate = false;
            try
            {
                await exit();
            }
            catch (System.OperationCanceledException) { throw; }
            catch (ApiException error)
            {
                reauthenticate = error.Code == "HTTP_401" || error.Code == "SESSION_EXPIRED";
                failure = reauthenticate
                    ? L10n.Get("menu.exitFailedRelogin", operation)
                    : L10n.Get("menu.exitFailedRetry", operation);
            }
            catch (System.Exception)
            {
                failure = L10n.Get("menu.exitFailedGeneric");
            }
            finally
            {
                if (progressLabel != null) { progressLabel.text = original; }
            }

            if (failure != null)
            {
                bool confirmed = await _confirm.ShowAsync(new ConfirmRequest
                {
                    Title = L10n.Get("menu.exitNeedsCheck", operation),
                    Message = failure,
                    ConfirmLabel = reauthenticate ? L10n.Get("menu.relogin") : L10n.Get("common.ok"),
                });
                // 로그인 방식과 게스트 ID를 지우면 새 게스트가 생길 수 있다. 기존 계정으로 다시 인증한다.
                if (reauthenticate && confirmed) { await _account.ReauthenticateAsync(); }
            }
        }

        private async UniTask LinkAccountAsync()
        {
            string notice = null;
            bool reauthenticate = false;
            switch (await _account.LinkAsync())
            {
                case LinkResult.Linked: notice = L10n.Get("menu.linkDone"); break;
                case LinkResult.AlreadyLinked: notice = L10n.Get("menu.linkAlready"); break;
                case LinkResult.Unavailable: notice = L10n.Get("menu.linkUnavailable"); break;
                case LinkResult.Conflict: notice = L10n.Get("menu.linkConflict"); break;
                case LinkResult.Failed: notice = L10n.Get("menu.linkFailed"); break;
                case LinkResult.ReauthenticationRequired:
                    notice = L10n.Get("menu.linkReauth");
                    reauthenticate = true;
                    break;
            }

            RefreshAccountButtons();
            if (notice != null)
            {
                bool confirmed = await _confirm.ShowAsync(new ConfirmRequest
                {
                    Title = L10n.Get("menu.linkNoticeTitle"),
                    Message = notice,
                    ConfirmLabel = reauthenticate ? L10n.Get("menu.relogin") : L10n.Get("common.ok"),
                });
                if (reauthenticate && confirmed) { await _account.ReauthenticateAsync(); }
            }
        }

        private void RefreshAccountButtons() => _linkAccountButton.gameObject.SetActive(_account.IsGuest);

        public void Open()
        {
            _versionText.text = $"Version {Application.version}";
            RefreshAccountButtons();
            PopupPanel.Set(_panel, _transition, true);
        }

        public void Close()
        {
            // 삭제 응답을 기다리는 동안 로비로 돌아가 새 진행을 쓰지 않게 한다.
            if (!_busy) { PopupPanel.Set(_panel, _transition, false); }
        }

        public void Toggle()
        {
            if (IsOpen) { Close(); } else { Open(); }
        }
    }
}
