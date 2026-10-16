using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;

namespace Game.Network
{
    /// <summary>전투 입장 한 번의 요청. 응답 유실·시간 초과 후 재시도에도 이 객체를 그대로 쓴다.
    /// 새 전투에만 새 객체를 만든다. 사용자 ID는 본문에 넣지 않고 Bearer 토큰으로 식별한다.</summary>
    [Serializable]
    public sealed class BattleStartRequest
    {
        public readonly string createId = Guid.NewGuid().ToString("D");
    }

    /// <summary>서버 PR #57의 세션 발급·종료·포기 계약. end는 보상 제출 API가 아니므로
    /// IBattleApi를 구현하거나 Local 전투에 섞지 않는다. Player 그룹 전환은 보상 계약 확정 후 연결한다.</summary>
    public sealed class HttpBattleSessionApi
    {
        private readonly HttpApiClient _client;

        public HttpBattleSessionApi(HttpApiClient client)
        {
            _client = client;
        }

        public UniTask<StartBattleResponse> StartBattle(BattleStartRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null) { throw new ArgumentNullException(nameof(request)); }
            return _client.Post<BattleStartRequest, StartBattleResponse>("api/battle/start", request, cancellationToken,
                isValid: response => !string.IsNullOrWhiteSpace(response.battleId));
        }

        /// <summary>세션만 종료한다. 보상·등급·진척은 반환하지 않는다. 실패 시 자동 재전송하지 않는다.</summary>
        public UniTask EndBattle(string battleId, CancellationToken cancellationToken = default) =>
            Finish("api/battle/end", battleId, cancellationToken);

        /// <summary>세션만 포기한다. 에너지 환불 계약은 없다. 실패 시 자동 재전송하지 않는다.</summary>
        public UniTask CancelBattle(string battleId, CancellationToken cancellationToken = default) =>
            Finish("api/battle/cancel", battleId, cancellationToken);

        private UniTask Finish(string path, string battleId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(battleId)) { throw new ArgumentException("battleId가 필요합니다", nameof(battleId)); }
            return _client.Post(path, new { battleId }, cancellationToken);
        }
    }
}
