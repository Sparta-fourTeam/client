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

        private const string SignInMessage = "로그인 방식을 선택해 주세요";
        private const string SigningInMessage = "로그인 중...";
        private const string FailedMessage = "로그인에 실패했습니다.\n잠시 후 다시 시도해 주세요.";
        private const string UnavailableSuffix = " (준비 중)";
        private const string GuestWarning = "게스트 계정은 앱을 삭제하거나 기기를 바꾸면 데이터가 사라질 수 있어요.";

        [SerializeField] private GameObject _panel;
        [SerializeField] private GameObject _methods;   // 로그인 방식 버튼 묶음
        [SerializeField] private Button _guestButton, _googleButton, _appleButton;
        [SerializeField] private Button _retryButton;
        [SerializeField] private TMP_Text _messageText;

        private ConfirmPopupView _confirm;
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

        // 생년월일·신분증을 수집하지 않는 이용자 자기확인이다. 매 로그인 흐름에서 확인하며
        // 약관 동의를 개인정보 처리 동의로 간주하지 않는다. 동의 전에는 LoginFlow가 인증하지 않는다.
        public async UniTask<bool> WaitForServiceConsentAsync()
        {
            while (true)
            {
                Show("서비스 이용 조건을 확인해 주세요.", methods: false, retry: false);
                bool ageConfirmed = await ShowConsentAsync(new ConfirmRequest
                {
                    Title = "이용 연령 확인",
                    Message = "끄적끄적 디펜스는 만 14세 이상 이용할 수 있어요.\n만 14세 이상인가요?",
                    ConfirmLabel = "만 14세 이상",
                    CancelLabel = "취소",
                });
                if (!ageConfirmed)
                {
                    await WaitForConditionsRetryAsync("만 14세 이상 확인과 약관 동의 전에는\n로그인을 진행하지 않아요.");
                    continue;
                }

                // 게시·승인 검증 후 URL을 넣기 전까지 동의를 받거나 인증을 진행하지 않는다.
                if (!IsDocumentAvailable(ExternalLinks.TermsOfUse) || !IsDocumentAvailable(ExternalLinks.PrivacyPolicy))
                {
                    await WaitForConditionsRetryAsync("서비스 이용 안내를 준비 중이에요.\n안내가 준비된 뒤 다시 이용해 주세요.");
                    continue;
                }

                bool termsOpened = await OpenDocumentAsync("이용약관 전문", ExternalLinks.TermsOfUse);
                bool privacyOpened = termsOpened && await OpenDocumentAsync("개인정보처리방침", ExternalLinks.PrivacyPolicy);
                if (privacyOpened && await ShowConsentAsync(new ConfirmRequest
                {
                    Title = "이용약관 동의",
                    Message = "이용약관 전문을 확인하고 동의하시나요?\n개인정보처리방침은 처리사항 안내이며,\n이 버튼은 별도 개인정보 동의를 대신하지 않아요.",
                    Warning = "테스트 진행 유지·정식 이전은 보장되지 않아요.\n계정 삭제에는 복구 유예가 없고,\n게스트 로그아웃도 삭제 확인으로 이어져요.",
                    ConfirmLabel = "약관에 동의",
                    CancelLabel = "동의하지 않음",
                }))
                {
                    Hide();
                    return true;
                }

                await WaitForConditionsRetryAsync("약관에 동의하지 않아 로그인을 진행하지 않았어요.\n다시 시도로 이용 조건을 확인할 수 있어요.");
            }
        }

        public async UniTask<bool> WaitForAnalyticsConsentAsync()
        {
            if (!await ShowConsentAsync(new ConfirmRequest
            {
                Title = "게임 이용 분석 (선택)",
                Message = "분석용 식별자·기기/앱/세션·국가 정보와\n전투 결과·도달 웨이브·시간·처치 수,\n스킬 피해량·카드 선택을 분석해\n이용 현황과 밸런스를 개선해요.\n처리 수탁자: GameAnalytics",
                Warning = "보관: 원시 기록 1개월, 분석 보관 1년,\n유지율 확인 기록 18개월\n동의하지 않아도 게임을 이용할 수 있어요.\nUnity 진단은 이 선택과 별개예요.",
                ConfirmLabel = "분석에 동의",
                CancelLabel = "동의하지 않음",
            })) { return false; }

            return await ShowConsentAsync(new ConfirmRequest
            {
                Title = "분석 정보 국외 이전 (선택)",
                Message = "받는 곳: GameAnalytics ApS 및 관계사\n미국 · privacy@gameanalytics.com\n게임 이용 중 SDK로 앞서 안내한 정보를\n분석 목적으로 네트워크 전송해요.",
                Warning = "보관: 원시 기록 1개월, 분석 보관 1년,\n유지율 확인 기록 18개월\n거부하면 분석을 사용하지 않고 게임은 가능해요.",
                ConfirmLabel = "국외 이전 동의",
                CancelLabel = "동의하지 않음",
            });
        }

        private async UniTask<bool> OpenDocumentAsync(string title, string url)
        {
            if (!await ShowConsentAsync(new ConfirmRequest
            {
                Title = title,
                Message = "브라우저에서 전체 내용을 확인해 주세요.\n페이지가 열리지 않으면 다음 단계에서 취소해 주세요.",
                ConfirmLabel = "전문 열기",
                CancelLabel = "취소",
            })) { return false; }
            Application.OpenURL(url);
            return true;
        }

        private UniTask WaitForConditionsRetryAsync(string message)
        {
            Show(message, methods: false, retry: true);
            return Wait().AsUniTask();
        }

        private static bool IsDocumentAvailable(string url) =>
            System.Uri.TryCreate(url, System.UriKind.Absolute, out var uri) &&
            uri.Scheme == System.Uri.UriSchemeHttps && uri.Host != "example.com";

        private UniTask<bool> ShowConsentAsync(ConfirmRequest request) =>
            _confirm.ShowAsync(request).AttachExternalCancellation(this.GetCancellationTokenOnDestroy());

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
                Title = "구글 로그인",
                Message = "구글 계정으로 로그인할까요?",
                ConfirmLabel = "로그인",
                CancelLabel = "취소",
            },
            LoginMethod.Apple => new ConfirmRequest
            {
                Title = "애플 로그인",
                Message = "애플 계정으로 로그인할까요?",
                ConfirmLabel = "로그인",
                CancelLabel = "취소",
            },
            _ => new ConfirmRequest
            {
                Title = "게스트 로그인",
                Message = "게스트로 로그인할까요?",
                Warning = GuestWarning,
                ConfirmLabel = "로그인",
                CancelLabel = "취소",
            },
        };

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
