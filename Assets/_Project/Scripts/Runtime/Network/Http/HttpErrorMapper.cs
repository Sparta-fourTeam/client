using System;
using Game.Core;

namespace Game.Network
{
    /// <summary>HTTP 응답을 ApiException으로 옮기는 규칙. 서버가 거절한 요청은 재시도해도 같으므로 Rejected,
    /// 시간이 지나면 풀릴 수 있는 것은 Transient, 닿지 못한 것은 Network로 나눈다</summary>
    public static class HttpErrorMapper
    {
        public const string NetworkError = "NETWORK_ERROR";
        public const string Timeout = "TIMEOUT";
        public const string InvalidResponse = "INVALID_RESPONSE";

        /// <summary>성공(2xx)이면 null, 아니면 던질 예외를 돌려준다.
        /// errorCodeReader는 응답 본문에서 서버의 오류 코드를 읽는 지점이다. 코드를 못 읽으면 HTTP_상태코드를 쓴다</summary>
        public static ApiException Map(HttpResponseData response, Func<int, string, string> errorCodeReader = null)
        {
            switch (response.Failure)
            {
                case HttpFailure.Timeout:
                    return new ApiException(ApiErrorKind.Transient, Timeout);
                case HttpFailure.Network:
                    return new ApiException(ApiErrorKind.Network, NetworkError);
            }

            var status = response.StatusCode;
            if (status >= 200 && status < 300)
            {
                return null;
            }

            var code = errorCodeReader?.Invoke(status, response.Body);
            if (string.IsNullOrEmpty(code))
            {
                code = $"HTTP_{status}";
            }

            return new ApiException(IsTransient(status) ? ApiErrorKind.Transient : ApiErrorKind.Rejected, code);
        }

        // 요청 시간 초과, 요청 과다, 서버 쪽 오류는 잠시 뒤 다시 보내면 성공할 수 있다
        private static bool IsTransient(int status) => status == 408 || status == 429 || status >= 500;
    }
}
