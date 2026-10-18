using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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

    /// <summary>서버가 해당 전투에서 한 번 지급한 불변 영수증. 현재 잔액과 구분한다.</summary>
    public sealed class BattleEndReceipt
    {
        public string BattleId { get; }
        public int StageId { get; }
        public int ClearedWave { get; }
        public int Rating { get; }
        public int Exp { get; }
        public int ThreeStarBonusExp { get; }
        public int Gold { get; }
        public IReadOnlyDictionary<string, int> Items { get; }
        public int ClearRating { get; }
        public DateTimeOffset CompletedAt { get; }

        private BattleEndReceipt(JObject body, Dictionary<string, int> items, DateTimeOffset completedAt)
        {
            BattleId = body.Value<string>("battleId");
            StageId = body.Value<int>("stageId");
            ClearedWave = body.Value<int>("clearedWave");
            Rating = body.Value<int>("rating");
            Exp = body.Value<int>("exp");
            ThreeStarBonusExp = body.Value<int>("threeStarBonusExp");
            Gold = body.Value<int>("gold");
            Items = new ReadOnlyDictionary<string, int>(items);
            ClearRating = body.Value<int>("clearRating");
            CompletedAt = completedAt;
        }

        internal static BattleEndReceipt Parse(string json, BattleEndRequest request)
        {
            try
            {
                using var reader = new JsonTextReader(new StringReader(json ?? string.Empty))
                { DateParseHandling = DateParseHandling.None };
                var body = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if (reader.Read() || body["battleId"]?.Type != JTokenType.String
                    || body.Value<string>("battleId") != request.battleId
                    || !Integer(body["stageId"], int.MinValue, int.MaxValue) || body.Value<int>("stageId") != request.stageId
                    || !Integer(body["clearedWave"], 0, 20) || body.Value<int>("clearedWave") != request.clearedWave
                    || !Integer(body["rating"], 0, 3) || body.Value<int>("rating") != request.rating
                    || !Integer(body["exp"], 0, int.MaxValue) || !Integer(body["gold"], 0, int.MaxValue)
                    || !Integer(body["threeStarBonusExp"], 0, body.Value<int>("exp"))
                    || (request.rating != 3 && body.Value<int>("threeStarBonusExp") != 0)
                    || !Integer(body["clearRating"], request.rating, 3)
                    || body["items"] is not JObject itemRows || body["completedAt"]?.Type != JTokenType.String
                    || !DateTimeOffset.TryParseExact(body.Value<string>("completedAt"),
                        new[] { "yyyy-MM-dd'T'HH:mm:ss'Z'", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'" },
                        CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var completedAt))
                { throw InvalidReceipt(); }
                var items = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var item in itemRows.Properties())
                {
                    if (string.IsNullOrWhiteSpace(item.Name) || !Integer(item.Value, 1, int.MaxValue)) { throw InvalidReceipt(); }
                    items.Add(item.Name, item.Value.Value<int>());
                }
                return new BattleEndReceipt(body, items, completedAt);
            }
            catch (JsonException) { throw InvalidReceipt(); }
        }

        private static bool Integer(JToken token, long min, long max) =>
            token?.Type == JTokenType.Integer
            && long.TryParse(token.ToString(Formatting.None), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            && value >= min && value <= max;
        private static ApiException InvalidReceipt() => new(ApiErrorKind.Rejected, HttpErrorMapper.InvalidResponse);
    }

    /// <summary>서버의 발급·결과 제출·포기 계약. end의 영수증은 현재 플레이어 스냅샷과 다르다.
    /// Player 그룹의 전체 정합성 확인 후 연결하며 Local 전투에 섞지 않는다.</summary>
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

        /// <summary>서버 #73 계약. 같은 결과 재요청은 최초 영수증을 반환한다. 자동 재전송은 하지 않는다.</summary>
        public async UniTask<BattleEndReceipt> EndBattle(BattleEndRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null) { throw new ArgumentNullException(nameof(request)); }
            var json = await _client.PostJson("api/battle/end", request, cancellationToken);
            return BattleEndReceipt.Parse(json, request);
        }

        /// <summary>세션만 포기한다. 에너지 환불 계약은 없다. 실패 시 자동 재전송하지 않는다.</summary>
        public UniTask CancelBattle(string battleId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(battleId)) { throw new ArgumentException("battleId가 필요합니다", nameof(battleId)); }
            return _client.Post("api/battle/cancel", new { battleId }, cancellationToken);
        }
    }
}
