using System;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Game.Network
{
    /// <summary>UnityWebRequest로 실제 요청을 보낸다. 서버 응답(4xx·5xx 포함)은 상태 코드와 본문으로 돌려주고,
    /// 응답을 받지 못한 경우만 HttpFailure로 구분한다. 취소하면 요청을 중단하고 OperationCanceledException을 던진다</summary>
    public sealed class UnityWebRequestTransport : IHttpTransport
    {
        public async UniTask<HttpResponseData> Send(HttpRequestData request, float timeoutSeconds, CancellationToken cancellationToken)
        {
            using var web = new UnityWebRequest(request.Url, request.Verb.ToString().ToUpperInvariant());
            web.downloadHandler = new DownloadHandlerBuffer();
            web.timeout = Mathf.CeilToInt(timeoutSeconds);

            if (request.Body != null)
            {
                web.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(request.Body));
                web.SetRequestHeader("Content-Type", "application/json");
            }

            web.SetRequestHeader("Accept", "application/json");
            if (request.Headers != null)
            {
                foreach (var header in request.Headers)
                {
                    web.SetRequestHeader(header.Key, header.Value);
                }
            }

            try
            {
                await web.SendWebRequest().WithCancellation(cancellationToken);
            }
            catch (UnityWebRequestException)
            {
                // 실패 종류는 아래에서 web.result로 가른다
            }

            switch (web.result)
            {
                case UnityWebRequest.Result.Success:
                case UnityWebRequest.Result.ProtocolError:
                    return new HttpResponseData((int)web.responseCode, web.downloadHandler.text);
                default:
                    return HttpResponseData.Failed(IsTimeout(web) ? HttpFailure.Timeout : HttpFailure.Network);
            }
        }

        // UnityWebRequest는 제한 시간 초과를 별도 결과로 주지 않고 오류 문구로만 알린다
        private static bool IsTimeout(UnityWebRequest web) =>
            web.error != null && web.error.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
