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
                    return "전투 정보가 올바르지 않거나 만료되어\n결과를 저장하지 못했어요.";
                default:
                    return string.IsNullOrEmpty(code)
                        ? "결과를 저장하지 못했어요."
                        : $"결과를 저장하지 못했어요.\n({code})";
            }
        }

        /// <summary>거절이 아닌 실패(네트워크, 일시적 오류, 예상 밖 예외). 다시 보낼 수 있을 때만 재시도를 권한다</summary>
        public static string ForFailed(bool retryable)
        {
            return retryable
                ? "네트워크나 서버 문제로\n결과를 보내지 못했어요.\n다시 시도해 볼까요?"
                : "예상하지 못한 문제로\n결과를 보내지 못했어요.";
        }
    }
}
