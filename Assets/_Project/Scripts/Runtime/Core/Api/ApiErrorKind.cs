namespace Game.Core
{
    /// <summary>API 실패를 분류하는 종류. 재시도 여부 판단에 쓰인다</summary>
    public enum ApiErrorKind
    {
        /// <summary>서버가 요청을 거절함 (재시도해도 같은 결과)</summary>
        Rejected,

        /// <summary>일시적 실패, 재시도하면 성공할 수 있음</summary>
        Transient,

        /// <summary>네트워크 자체가 끊긴 상태</summary>
        Network
    }
}
