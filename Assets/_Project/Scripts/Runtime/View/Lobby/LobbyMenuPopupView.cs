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
            else { button.GetComponentInChildren<TMP_Text>(true).text += " (준비 중)"; }
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
                Title = "문의하기",
                Message = $"메일 작성 화면이 열려요.\n열리지 않으면 문의 양식이 복사되어 있으니\n{SupportMail.Address}로 붙여 넣어 보내 주세요.",
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
                Title = "로그아웃",
                Message = "로그아웃하시겠습니까?",
                Warning = _account.IsGuest ? "게스트로 플레이 중이라면 모든 데이터가 삭제되며 복구할 수 없습니다." : null,
                ConfirmLabel = "로그아웃",
                CancelLabel = "취소",
            };
            if (await _confirm.ShowAsync(request))
            {
                if (_account.IsGuest) { await DeleteAccountAsync(); }
                else { await ExecuteAccountExitAsync(_account.LogoutAsync, "로그아웃"); }
            }
        }

        private async UniTask DeleteAccountAsync()
        {
            var first = new ConfirmRequest
            {
                Title = "계정을 삭제하시겠습니까?",
                Warning = "계정과 진행 데이터가 즉시 삭제되며 복구할 수 없습니다. 같은 Google 계정으로 다시 가입해도 처음부터 시작합니다.",
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
                Message = $"복구 유예 없이 모든 진행 데이터가 삭제됩니다.\n삭제하려면 아래 닉네임을 입력해 주세요.\n\n<noparse>{_profile.Nickname}</noparse>",
                ConfirmLabel = "계정 삭제",
                CancelLabel = "취소",
                InputPlaceholder = "닉네임을 입력하세요",
                RequiredInput = _profile.Nickname,
            };
            if (await _confirm.ShowAsync(second))
            {
                await ExecuteAccountExitAsync(_account.DeleteAccountAsync, "계정 삭제",
                    _deleteAccountButton.GetComponentInChildren<TMP_Text>(true));
            }
        }

        private async UniTask ExecuteAccountExitAsync(System.Func<UniTask> exit, string operation, TMP_Text progressLabel = null)
        {
            string original = progressLabel != null ? progressLabel.text : null;
            if (progressLabel != null) { progressLabel.text = "삭제 중…"; }
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
                    ? $"{operation} 완료를 확인하지 못했습니다. 로그인 정보를 다시 확인한 뒤 시도해 주세요."
                    : $"{operation} 완료를 확인하지 못했습니다. 연결 상태를 확인하고 다시 시도해 주세요. 문제가 계속되면 문의해 주세요.";
            }
            catch (System.Exception)
            {
                failure = "계정 작업을 완료하지 못했습니다. 앱을 다시 실행한 뒤 확인해 주세요. 문제가 계속되면 문의해 주세요.";
            }
            finally
            {
                if (progressLabel != null) { progressLabel.text = original; }
            }

            if (failure != null)
            {
                bool confirmed = await _confirm.ShowAsync(new ConfirmRequest
                {
                    Title = $"{operation} 확인 필요",
                    Message = failure,
                    ConfirmLabel = reauthenticate ? "다시 로그인" : "확인",
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
                case LinkResult.Linked: notice = "계정이 연동되었습니다."; break;
                case LinkResult.AlreadyLinked: notice = "이미 연동된 계정입니다."; break;
                case LinkResult.Unavailable: notice = "연동할 수 있는 계정이 아직 없습니다. 준비 중입니다."; break;
                case LinkResult.Conflict: notice = "이미 연동된 계정이거나, 다른 계정에 연동된 구글 계정입니다."; break;
                case LinkResult.Failed: notice = "계정을 연동하지 못했습니다. 잠시 후 다시 시도해 주세요."; break;
                case LinkResult.ReauthenticationRequired:
                    notice = "계정 연동을 완료하지 못했습니다. 기존 계정으로 다시 로그인한 뒤 연동해 주세요.";
                    reauthenticate = true;
                    break;
            }

            RefreshAccountButtons();
            if (notice != null)
            {
                bool confirmed = await _confirm.ShowAsync(new ConfirmRequest
                {
                    Title = "알림",
                    Message = notice,
                    ConfirmLabel = reauthenticate ? "다시 로그인" : "확인",
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
