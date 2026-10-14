using System;

namespace Game.Core
{
    /// <summary>API 호출 실패를 나타내는 예외. 서버와 Local 백엔드가 같은 오류 코드로 던진다</summary>
    public sealed class ApiException : Exception
    {
        public ApiException(ApiErrorKind kind, string code, string detail = null)
            : base(string.IsNullOrEmpty(detail) ? $"{kind}: {code}" : $"{kind}: {code} ({detail})")
        {
            Kind = kind;
            Code = code;
            Detail = detail;
        }

        /// <summary>실패 종류 (거절/일시적/네트워크)</summary>
        public ApiErrorKind Kind { get; }

        /// <summary>서버와 공유하는 오류 코드 (예: INSUFFICIENT_GOLD)</summary>
        public string Code { get; }

        /// <summary>서버가 응답 본문에 적어 준 설명. 로그로 원인을 보려는 것이라 화면에 쓰거나 분기에 쓰지 않는다. 없으면 null</summary>
        public string Detail { get; }
    }
}
