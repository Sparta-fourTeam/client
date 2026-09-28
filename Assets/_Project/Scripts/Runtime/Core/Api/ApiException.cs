using System;

namespace Game.Core
{
    /// <summary>API 호출 실패를 나타내는 예외. 서버와 Local 백엔드가 같은 오류 코드로 던진다</summary>
    public sealed class ApiException : Exception
    {
        /// <summary>실패 종류 (거절/일시적/네트워크)</summary>
        public ApiErrorKind Kind;

        /// <summary>서버와 공유하는 오류 코드 (예: INSUFFICIENT_GOLD)</summary>
        public string Code;
    }
}
