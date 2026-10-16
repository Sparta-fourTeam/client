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

    /// <summary>서버 PR #59의 전투 결과. stageId는 서버 카탈로그의 정수 ID를 사용한다.</summary>
    [Serializable]
    public sealed class BattleEndRequest
    {
        public readonly string battleId;
        public readonly int stageId;
        public readonly int clearedWave;
        public readonly int rating;

        public BattleEndRequest(string battleId, int stageId, int clearedWave, int rating)
        {
            if (string.IsNullOrWhiteSpace(battleId)) { throw new ArgumentException("battleId가 필요합니다", nameof(battleId)); }
            if (clearedWave < 0 || clearedWave > 20) { throw new ArgumentOutOfRangeException(nameof(clearedWave)); }
            if (rating < 0 || rating > 3) { throw new ArgumentOutOfRangeException(nameof(rating)); }
            this.battleId = battleId;
            this.stageId = stageId;
            this.clearedWave = clearedWave;
            this.rating = rating;
        }
    }

    /// <summary>서버의 발급·결과 제출·포기 계약. end는 보상·진척을 변경하지만 스냅샷을 반환하지 않으므로
    /// IBattleApi를 구현하거나 Local 전투에 섞지 않는다. Player 그룹의 전체 정합성 확인 후 연결한다.</summary>
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

        /// <summary>서버에서 보상 지급·진척 갱신 후 세션을 삭제한다. 성공 본문은 없다.
        /// 네트워크 실패 뒤 자동 재전송하거나 404를 성공으로 바꾸지 않는다.</summary>
        public UniTask EndBattle(BattleEndRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null) { throw new ArgumentNullException(nameof(request)); }
            return _client.Post("api/battle/end", request, cancellationToken);
        }

        /// <summary>세션만 포기한다. 에너지 환불 계약은 없다. 실패 시 자동 재전송하지 않는다.</summary>
        public UniTask CancelBattle(string battleId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(battleId)) { throw new ArgumentException("battleId가 필요합니다", nameof(battleId)); }
            return _client.Post("api/battle/cancel", new { battleId }, cancellationToken);
        }
    }
}
