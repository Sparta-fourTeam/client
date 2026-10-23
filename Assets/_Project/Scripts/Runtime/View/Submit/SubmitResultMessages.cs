using Game.Core;

namespace Game.View
{
    /// <summary>결과 제출 거절과 실패 코드를 플레이어가 읽을 문구로 바꾼다. 모르는 코드는 코드를 그대로 보여준다.
    /// 서버가 제출에서 돌려주는 코드가 더 정해지면 여기에 추가한다</summary>
    public static class SubmitResultMessages
    {
        public static string ForRejected(string code)
        {
            switch (code)
            {
                case "INVALID_ID":
                    return L10n.Get("submit.invalidBattle");
                default:
                    return string.IsNullOrEmpty(code)
                        ? L10n.Get("submit.saveFailed")
                        : L10n.Get("submit.saveFailedCode", code);
            }
        }

        /// <summary>거절이 아닌 실패(네트워크, 일시적 오류, 예상 밖 예외). 다시 보낼 수 있을 때만 재시도를 권한다</summary>
        public static string ForFailed(bool retryable)
        {
            return retryable
                ? L10n.Get("submit.networkFailed")
                : L10n.Get("submit.unexpectedFailed");
        }
    }
}
