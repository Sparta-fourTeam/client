using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.Network
{
    /// <summary>서버 #79 계약. 서버에서 조회한 기간키를 요청에 고정하며 Local의 IQuestApi와 혼합하지 않는다.</summary>
    public sealed class HttpQuestApi
    {
        private readonly HttpApiClient _client;
        public HttpQuestApi(HttpApiClient client) => _client = client ?? throw new ArgumentNullException(nameof(client));

        public async UniTask<List<ServerQuestBoard>> GetBoards(CancellationToken cancellationToken = default)
        {
            var json = await _client.GetJson("api/user/quests", cancellationToken);
            return ServerProgressContract.Parse(() => ServerProgressContract.ReadBoards(json));
        }

        public UniTask<QuestClaimReceipt> ClaimQuest(QuestClaimRequest request, CancellationToken cancellationToken = default) =>
            Claim(request, false, cancellationToken);

        public UniTask<QuestClaimReceipt> ClaimChest(QuestClaimRequest request, CancellationToken cancellationToken = default) =>
            Claim(request, true, cancellationToken);

        private async UniTask<QuestClaimReceipt> Claim(QuestClaimRequest request, bool chest, CancellationToken cancellationToken)
        {
            if (request == null) { throw new ArgumentNullException(nameof(request)); }
            var json = await _client.PostJson(chest ? "api/user/quests/chests/claim" : "api/user/quests/claim", request, cancellationToken);
            return ServerProgressContract.Parse(() => ServerProgressContract.ReadQuestReceipt(json, request, chest));
        }
    }
}
