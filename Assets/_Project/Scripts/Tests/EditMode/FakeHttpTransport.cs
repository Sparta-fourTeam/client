using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Network;

namespace Game.Tests
{
    /// <summary>실제 통신 없이 정해 둔 응답이나 예외를 돌려주고, 받은 요청을 기록하는 가짜 전송</summary>
    public sealed class FakeHttpTransport : IHttpTransport
    {
        public HttpResponseData Response = new HttpResponseData(200, "{}");
        public Exception ToThrow;
        public readonly List<HttpRequestData> Requests = new();
        public float LastTimeout;

        public UniTask<HttpResponseData> Send(HttpRequestData request, float timeoutSeconds, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            LastTimeout = timeoutSeconds;
            cancellationToken.ThrowIfCancellationRequested();
            if (ToThrow != null)
            {
                throw ToThrow;
            }

            return UniTask.FromResult(Response);
        }
    }
}
