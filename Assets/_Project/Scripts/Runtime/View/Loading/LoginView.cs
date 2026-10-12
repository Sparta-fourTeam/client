using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>Loading 화면의 로그인창. 최초 진입이면 로그인 방식 버튼(게스트·구글·애플)을, 로그인에 실패하면 [다시 시도]를 보여주고 누를 때까지 기다린다.
    /// 쓸 수 없는 방식의 버튼은 "(준비 중)"을 붙여 막는다. 창이 떠 있는 동안 로그인 중이면 버튼을 숨기고 진행 문구만 보여준다.
    /// 패널과 [다시 시도]는 시작할 때 숨긴다(UiInitialVisibility)</summary>
    public sealed class LoginView : HudView, ILoginPrompt
    {
        private const string SignInMessage = "로그인 방식을 선택해 주세요";
        private const string SigningInMessage = "로그인 중...";
        private const string FailedMessage = "로그인에 실패했습니다.\n잠시 후 다시 시도해 주세요.";
        private const string UnavailableSuffix = " (준비 중)";

        [SerializeField] private GameObject _panel;
        [SerializeField] private GameObject _methods;   // 로그인 방식 버튼 묶음
        [SerializeField] private Button _guestButton, _googleButton, _appleButton;
        [SerializeField] private Button _retryButton;
        [SerializeField] private TMP_Text _messageText;

        private readonly Dictionary<Button, string> _labels = new();
        private UniTaskCompletionSource<LoginMethod> _pending;

        protected override void InitializeView()
        {
            _panel.SetActive(false);
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

        public UniTask<LoginMethod> WaitForSignInAsync(IReadOnlyCollection<LoginMethod> available)
        {
            Show(SignInMessage, methods: true, retry: false);
            SetAvailable(_guestButton, LoginMethod.Guest, available);
            SetAvailable(_googleButton, LoginMethod.Google, available);
            SetAvailable(_appleButton, LoginMethod.Apple, available);
            return Wait();
        }

        public UniTask WaitForRetryAsync()
        {
            Show(FailedMessage, methods: false, retry: true);
            return Wait().AsUniTask();
        }

        // 저장된 로그인 정보로 바로 로그인할 때는 창을 띄우지 않는다(Loading 화면만 보인다).
        // 로그인창이나 재시도 창이 떠 있을 때만 진행 문구로 바꾼다
        public void ShowSigningIn()
        {
            if (_panel.activeSelf)
            {
                Show(SigningInMessage, methods: false, retry: false);
            }
        }

        public void Hide()
        {
            _panel.SetActive(false);
        }

        private void Bind(Button button, LoginMethod method)
        {
            _labels[button] = button.GetComponentInChildren<TMP_Text>(true).text;
            button.onClick.AddListener(() => Complete(method));
        }

        private void SetAvailable(Button button, LoginMethod method, IReadOnlyCollection<LoginMethod> available)
        {
            bool on = Contains(available, method);
            button.interactable = on;
            button.GetComponentInChildren<TMP_Text>(true).text = on ? _labels[button] : _labels[button] + UnavailableSuffix;
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

        private void Show(string message, bool methods, bool retry)
        {
            _panel.SetActive(true);
            _messageText.text = message;
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
