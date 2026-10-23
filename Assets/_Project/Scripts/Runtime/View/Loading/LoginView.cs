using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>Loading 화면의 로그인창. 최초 진입이면 로그인 방식 버튼(구글·애플·게스트)을, 로그인에 실패하면 [다시 시도]를 보여주고 누를 때까지 기다린다.
    /// 방식을 누르면 확인창에서 한 번 더 묻고, 게스트는 데이터가 사라질 수 있다는 경고를 함께 보여준다. 취소하면 방식 선택으로 돌아간다.
    /// 쓸 수 없는 방식의 버튼은 "(준비 중)"을 붙여 막는다. 창이 떠 있는 동안 로그인 중이면 버튼을 숨기고 진행 문구만 보여준다.
    /// 패널과 [다시 시도]는 시작할 때 숨긴다(UiInitialVisibility)</summary>
    public sealed class LoginView : HudView, ILoginPrompt, IUiBindable
    {
        public void BindUi(IUiRegistry registry) => _confirm = registry.Get<ConfirmPopupView>();

        // 로케일이 바뀔 수 있어 상수 대신 호출 시점에 조회한다
        private static string SignInMessage => L10n.Get("login.chooseMethod");
        private static string SigningInMessage => L10n.Get("login.signingIn");
        private static string FailedMessage => L10n.Get("login.failed");
        private static string GuestWarning => L10n.Get("login.guestWarning");

        [SerializeField] private GameObject _panel;
        [SerializeField] private GameObject _methods;   // 로그인 방식 버튼 묶음
        [SerializeField] private Button _guestButton, _googleButton, _appleButton;
        [SerializeField] private Button _retryButton;
        [SerializeField] private TMP_Text _messageText;

        [SerializeField] private ServiceConsentView _serviceConsent;

        private ConfirmPopupView _confirm;
        private readonly Dictionary<Button, string> _labelKeys = new();
        private UniTaskCompletionSource<LoginMethod> _pending;

        protected override void InitializeView()
        {
            _panel.SetActive(false);
            _serviceConsent.Initialize();
            Bind(_guestButton, LoginMethod.Guest);
            Bind(_googleButton, LoginMethod.Google);
            Bind(_appleButton, LoginMethod.Apple);
            _retryButton.onClick.AddListener(() => Complete(LoginMethod.Guest)); // 재시도에서는 고른 방식을 쓰지 않는다
        }

        // 화면이 사라지면(씬 전환 등) 기다리던 흐름도 끝낸다
        protected override void DisposeView()
        {
            _pending?.TrySetCanceled();
            _pending = null;
        }

        public UniTask<LoginConsent> WaitForConsentAsync() => _serviceConsent.ShowAsync();

        public UniTask<LoginMethod> WaitForSignInAsync(IReadOnlyCollection<LoginMethod> available)
        {
            Show(() => SignInMessage, methods: true, retry: false);
            SetAvailable(_guestButton, LoginMethod.Guest, available);
            SetAvailable(_googleButton, LoginMethod.Google, available);
            SetAvailable(_appleButton, LoginMethod.Apple, available);
            return Wait();
        }

        public UniTask WaitForRetryAsync()
        {
            Show(() => FailedMessage, methods: false, retry: true);
            return Wait().AsUniTask();
        }

        // 저장된 로그인 정보로 바로 로그인할 때는 창을 띄우지 않는다(Loading 화면만 보인다).
        // 로그인창이나 재시도 창이 떠 있을 때만 진행 문구로 바꾼다
        public void ShowSigningIn()
        {
            if (_panel.activeSelf)
            {
                Show(() => SigningInMessage, methods: false, retry: false);
            }
        }

        public void Hide()
        {
            _panel.SetActive(false);
        }

        private static string LabelKey(LoginMethod method) => method switch
        {
            LoginMethod.Google => "login.google",
            LoginMethod.Apple => "login.apple",
            _ => "login.guest",
        };

        private void Bind(Button button, LoginMethod method)
        {
            _labelKeys[button] = LabelKey(method);
            button.onClick.AddListener(() => ConfirmAsync(method).Forget());
        }

        // 고른 방식으로 로그인할지 확인창에서 묻고, 확인하면 그 방식으로 끝낸다. 확인창이 떠 있는 동안 뒤의 버튼은 가려진다
        private async UniTaskVoid ConfirmAsync(LoginMethod method)
        {
            if (_pending == null)
            {
                return;
            }

            bool confirmed = await _confirm.ShowAsync(Request(method));
            if (confirmed)
            {
                Complete(method);
            }
        }

        private static ConfirmRequest Request(LoginMethod method) => method switch
        {
            LoginMethod.Google => new ConfirmRequest
            {
                Title = L10n.Get("login.google.title"),
                Message = L10n.Get("login.google.message"),
                ConfirmLabel = L10n.Get("login.confirm"),
                CancelLabel = L10n.Get("common.cancel"),
            },
            LoginMethod.Apple => new ConfirmRequest
            {
                Title = L10n.Get("login.apple.title"),
                Message = L10n.Get("login.apple.message"),
                ConfirmLabel = L10n.Get("login.confirm"),
                CancelLabel = L10n.Get("common.cancel"),
            },
            _ => new ConfirmRequest
            {
                Title = L10n.Get("login.guest.title"),
                Message = L10n.Get("login.guest.message"),
                Warning = GuestWarning,
                ConfirmLabel = L10n.Get("login.confirm"),
                CancelLabel = L10n.Get("common.cancel"),
            },
        };

        private void SetAvailable(Button button, LoginMethod method, IReadOnlyCollection<LoginMethod> available)
        {
            bool on = Contains(available, method);
            button.interactable = on;
            var label = button.GetComponentInChildren<TMP_Text>(true);
            string key = _labelKeys[button];
            if (on) { label.SetL10n(key); }
            else { label.SetL10n(() => L10n.Get("login.unavailable", L10n.Get(key))); }
        }

        private static bool Contains(IReadOnlyCollection<LoginMethod> methods, LoginMethod method)
        {
            foreach (var m in methods)
            {
                if (m == method)
                {
                    return true;
                }
            }

            return false;
        }

        private UniTask<LoginMethod> Wait()
        {
            _pending = new UniTaskCompletionSource<LoginMethod>();
            return _pending.Task;
        }

        private void Show(Func<string> message, bool methods, bool retry)
        {
            _panel.SetActive(true);
            _messageText.SetL10n(message);
            _methods.SetActive(methods);
            _retryButton.gameObject.SetActive(retry);
        }

        private void Complete(LoginMethod method)
        {
            var pending = _pending;
            _pending = null;
            pending?.TrySetResult(method);
        }
    }
}
