using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Game.Core
{
    /// <summary>명시적으로 확인한 이용 조건과 서로 독립적인 선택 동의.</summary>
    public readonly struct LoginConsent
    {
        public const string Revision = "terms-2026-10-16_compact-1";
        public readonly bool TermsAccepted;
        public readonly bool AnalyticsAccepted;
        public readonly bool AnalyticsTransferAccepted;
        public bool AllowsAnalytics => TermsAccepted && AnalyticsAccepted && AnalyticsTransferAccepted;

        public LoginConsent(bool termsAccepted, bool analyticsAccepted, bool analyticsTransferAccepted)
        {
            TermsAccepted = termsAccepted;
            AnalyticsAccepted = analyticsAccepted;
            AnalyticsTransferAccepted = analyticsTransferAccepted;
        }
    }

    /// <summary>이 기기의 현재 고지 버전에 대한 선택. 계정·생년월일을 저장하지 않는다.</summary>
    public interface ILoginConsentStore
    {
        bool TryLoad(out LoginConsent consent);
        void Save(LoginConsent consent);
        void Clear();
    }

    /// <summary>로그인 화면. 로그인 흐름(LoginFlow)이 사용자의 선택을 기다릴 때 쓴다</summary>
    public interface ILoginPrompt
    {
        /// <summary>기본 미선택인 분석 선택 후 필수 약관을 확인한다.</summary>
        UniTask<LoginConsent> WaitForConsentAsync();

        /// <summary>로그인창을 보여주고 로그인 방식 버튼을 누를 때까지 기다린다. available에 없는 방식은 누를 수 없다</summary>
        UniTask<LoginMethod> WaitForSignInAsync(IReadOnlyCollection<LoginMethod> available);

        /// <summary>실패 문구와 [다시 시도]를 보여주고 누를 때까지 기다린다</summary>
        UniTask WaitForRetryAsync();

        /// <summary>로그인 요청 중임을 보여준다 (버튼은 누를 수 없다)</summary>
        void ShowSigningIn();

        /// <summary>로그인 화면을 닫는다</summary>
        void Hide();
    }
}
