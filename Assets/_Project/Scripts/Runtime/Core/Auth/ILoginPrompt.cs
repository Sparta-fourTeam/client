using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Game.Core
{
    /// <summary>로그인 화면. 로그인 흐름(LoginFlow)이 사용자의 선택을 기다릴 때 쓴다</summary>
    public interface ILoginPrompt
    {
        /// <summary>인증 전에 이용 연령과 약관을 확인한다. 거부·미공개 상태에서는 인증을 시작하지 않는다</summary>
        UniTask<bool> WaitForServiceConsentAsync();

        /// <summary>선택적 분석 수집·국외 이전 동의를 확인한다. 거절해도 게임을 이용할 수 있다.</summary>
        UniTask<bool> WaitForAnalyticsConsentAsync();

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
