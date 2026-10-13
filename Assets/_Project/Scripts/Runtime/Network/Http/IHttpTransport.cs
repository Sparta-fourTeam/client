using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.Network
{
    public enum HttpVerb
    {
        Get,
        Post,
        Put,
        Delete
    }

    /// <summary>전송 단계에서 응답을 받지 못한 이유. HTTP 상태 코드로 설명되는 실패는 여기에 넣지 않는다</summary>
    public enum HttpFailure
    {
        None,

        /// <summary>연결이 끊겼거나 서버에 닿지 못함</summary>
        Network,

        /// <summary>제한 시간 안에 응답이 오지 않음</summary>
        Timeout
    }

    public readonly struct HttpRequestData
    {
        public HttpRequestData(HttpVerb verb, string url, IReadOnlyDictionary<string, string> headers, string body)
        {
            Verb = verb;
            Url = url;
            Headers = headers;
            Body = body;
        }

        public HttpVerb Verb { get; }
        public string Url { get; }
        public IReadOnlyDictionary<string, string> Headers { get; }

        /// <summary>JSON 본문. 본문이 없는 요청이면 null</summary>
        public string Body { get; }
    }

    public readonly struct HttpResponseData
    {
        public HttpResponseData(int statusCode, string body, HttpFailure failure = HttpFailure.None)
        {
            StatusCode = statusCode;
            Body = body;
            Failure = failure;
        }

        public int StatusCode { get; }
        public string Body { get; }
        public HttpFailure Failure { get; }

        public static HttpResponseData Failed(HttpFailure failure) => new HttpResponseData(0, null, failure);
    }

    /// <summary>실제 HTTP 전송. 테스트에서는 가짜 구현으로 바꿔 정상·실패·타임아웃을 재현한다</summary>
    public interface IHttpTransport
    {
        /// <summary>취소되면 OperationCanceledException을 던진다. 응답을 받지 못한 실패는 던지지 않고 Failure로 돌려준다</summary>
        UniTask<HttpResponseData> Send(HttpRequestData request, float timeoutSeconds, CancellationToken cancellationToken);
    }
}
