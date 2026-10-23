using System;
using Game.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Game.Network
{
    /// <summary>HTTP 응답을 ApiException으로 옮기는 규칙. 서버가 거절한 요청은 재시도해도 같으므로 Rejected,
    /// 시간이 지나면 풀릴 수 있는 것은 Transient, 닿지 못한 것은 Network로 나눈다</summary>
    public static class HttpErrorMapper
    {
        public const string NetworkError = "NETWORK_ERROR";
        public const string Timeout = "TIMEOUT";
        public const string InvalidResponse = "INVALID_RESPONSE";

        /// <summary>재발급이 거절되거나 결과가 불명확해 토큰을 재사용할 수 없으므로 다시 로그인해야 한다. 호출부는 이 코드를 받으면 로그인으로 돌려보낸다</summary>
        public const string SessionExpired = "SESSION_EXPIRED";

        /// <summary>서버가 요청 내용을 확정적으로 거절했을 때만 로컬 재시도 키를 버린다.</summary>
        public static bool IsDefinitiveRequestRejection(ApiException error)
        {
            if (error?.Kind != ApiErrorKind.Rejected || error.Code == null
                || !error.Code.StartsWith("HTTP_", StringComparison.Ordinal)
                || !int.TryParse(error.Code.Substring(5), out int status)) { return false; }
            return status >= 400 && status < 500 && status != 401 && status != 403 && status != 408 && status != 429;
        }

        private const int MaxDetailLength = 200;

        private static string ReadPolicyCode(string body)
        {
            try
            {
                string code = JObject.Parse(body ?? "").Value<string>("code");
                return code is "MAINTENANCE" or "DRAINING" or "CLIENT_UPDATE_REQUIRED" ? code : null;
            }
            catch (JsonException) { return null; }
        }

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

            string policyCode = ReadPolicyCode(response.Body);
            if (policyCode != null)
            { return new ApiException(ApiErrorKind.Transient, policyCode, ReadDetail(response.Body)); }
            var code = errorCodeReader?.Invoke(status, response.Body);
            if (string.IsNullOrEmpty(code))
            {
                code = $"HTTP_{status}";
            }

            return new ApiException(IsTransient(status) ? ApiErrorKind.Transient : ApiErrorKind.Rejected, code, ReadDetail(response.Body));
        }

        // 서버가 오류 본문에 적어 주는 "message"를 로그에 남기려고 읽는다. 코드로 분기하지는 않는다. JSON이 아니면 없는 것으로 본다
        private static string ReadDetail(string body)
        {
            if (string.IsNullOrEmpty(body) || !body.TrimStart().StartsWith("{", StringComparison.Ordinal))
            {
                return null;
            }

            try
            {
                string message = JObject.Parse(body).Value<string>("message");
                if (string.IsNullOrWhiteSpace(message))
                {
                    return null;
                }

                return message.Length <= MaxDetailLength ? message : message.Substring(0, MaxDetailLength);
            }
            catch (JsonException)
            {
                return null;
            }
            catch (FormatException)
            {
                return null;
            }
        }

        // 요청 시간 초과, 요청 과다, 서버 쪽 오류는 잠시 뒤 다시 보내면 성공할 수 있다
        private static bool IsTransient(int status) => status == 408 || status == 429 || status >= 500;
    }
}
