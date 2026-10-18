using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.Network
{
    /// <summary>서버 #78 원형 계약. ITutorialApi의 현재 스냅샷 응답과 달리 최초 영수증을 반환한다.</summary>
    public sealed class HttpTutorialApi
    {
        private readonly HttpApiClient _client;
        public HttpTutorialApi(HttpApiClient client) => _client = client ?? throw new ArgumentNullException(nameof(client));

        public async UniTask<List<ServerTutorialState>> GetStates(CancellationToken cancellationToken = default)
        {
            var json = await _client.GetJson("api/user/tutorials", cancellationToken);
            return ServerProgressContract.Parse(() => ServerProgressContract.ReadTutorials(json));
        }

        /// <summary>시청·완료 요청은 ID만 전송한다. 재시도에는 처음 만든 요청을 그대로 사용한다.</summary>
        public async UniTask<TutorialCompletionReceipt> Complete(TutorialCompletionRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null) { throw new ArgumentNullException(nameof(request)); }
            var json = await _client.PostJson("api/user/tutorials/complete", request, cancellationToken);
            return ServerProgressContract.Parse(() => ServerProgressContract.ReadTutorialReceipt(json, request));
        }
    }
}
