using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Cysharp.Threading.Tasks;
using Game.Core;
using Newtonsoft.Json.Linq;

namespace Game.Network
{
    /// <summary>중간 보고의 서버 스테이지 고정·최초 ACK 계약. Root의 Local/HTTP 그룹 전환은 하지 않는다.</summary>
    public sealed class HttpBattleProgressApi : IBattleProgressApi
    {
        private readonly HttpApiClient _client;
        private readonly int _stageId;
        private readonly string _sourceRevision;
        private string _openedBattle;
        public string SourceRevision => _sourceRevision;
        public HttpBattleProgressApi(HttpApiClient client, int stageId, GameDataStore data)
        {
            _client = client; _stageId = stageId;
            _sourceRevision = (data ?? throw new ArgumentNullException(nameof(data))).BattleSourceRevision;
        }

        /// <summary>전투 시작 전에 실제 로드한 원문 버전으로 최초 고정한다.</summary>
        public async UniTask Bind(string battleId)
        {
            var openedJson = await _client.PostJson("api/battle/progress/open", new { battleId, stageId = _stageId, sourceRevision = _sourceRevision });
            ServerProgressContract.Parse(() =>
            {
                if (ServerProgressContract.Read(openedJson) is not JObject opened || opened["closed"]?.Type != JTokenType.Boolean
                    || opened.Value<bool>("closed") || opened["progress"]?.Value<string>("battleId") != battleId
                    || opened["progress"]?.Value<string>("sourceRevision") != _sourceRevision
                    || !Integer(opened["progress"]?["stageId"], _stageId, _stageId)) { throw ServerProgressContract.Invalid(); }
                return true;
            });
            _openedBattle = battleId;
        }

        public UniTask<BattleProgressAck> ReportProgress(BattleProgressRequest request) => Send(request, false);
        public UniTask<BattleProgressAck> AbandonProgress(BattleProgressRequest request) => Send(request, true);

        private async UniTask<BattleProgressAck> Send(BattleProgressRequest request, bool abandon)
        {
            if (request == null) { throw new ArgumentNullException(nameof(request)); }
            if (request.sourceRevision != _sourceRevision)
            {
                // 원본 갱신 전 이미 고정된 전투는 서버 원장을 확인한 뒤 같은 원문 보고만 복구한다.
                var saved = await _client.GetJson("api/battle/progress/" + Uri.EscapeDataString(request.battleId));
                ServerProgressContract.Parse(() =>
                {
                    var state = ServerProgressContract.Read(saved);
                    if (state?["progress"]?.Value<string>("battleId") != request.battleId
                        || state?["progress"]?.Value<string>("sourceRevision") != request.sourceRevision
                        || !Integer(state?["progress"]?["stageId"], _stageId, _stageId))
                    { throw new ApiException(ApiErrorKind.Rejected, "BATTLE_SOURCE_MISMATCH"); }
                    return true;
                });
            }
            string json;
            var path = abandon ? "api/battle/progress/abandon" : "api/battle/progress";
            try { json = await _client.PostJson(path, request); }
            catch (ApiException error) when (error.Code == "HTTP_404" && _openedBattle != request.battleId && request.sourceRevision == _sourceRevision)
            {
                // 원장이 없다는 최초 404만 open 뒤 재전송한다. 닫힌 전투의 기존 보고는 open 없이 원래 ACK를 읽는다.
                await Bind(request.battleId);
                json = await _client.PostJson(path, request);
            }
            return ServerProgressContract.Parse(() => ParseAck(json, request));
        }

        private BattleProgressAck ParseAck(string json, BattleProgressRequest request)
        {
            if (ServerProgressContract.Read(json) is not JObject ack || ack.Value<string>("battleId") != request.battleId
                || !Integer(ack["stageId"], _stageId, _stageId) || !Integer(ack["policyVersion"], 1)
                || ack.Value<string>("sourceRevision") != request.sourceRevision
                || !Integer(ack["revision"], request.expectedRevision + 1, request.expectedRevision + 1)
                || !Integer(ack["killedMonsters"], request.killedMonsters, request.killedMonsters)
                || !Integer(ack["completedWaves"], request.completedWaves, request.completedWaves)
                || !Integer(ack["reachedWave"], request.reachedWave, request.reachedWave)
                || !Integer(ack["acceptedDelta"], 0, request.killedMonsters)
                || ack["encounteredMonsterIds"] is not JArray ids || !ids.All(id => Integer(id, 1))
                || ids.Select(id => id.Value<int>()).Distinct().Count() != ids.Count
                || !new HashSet<int>(request.encounteredMonsterIds).SetEquals(ids.Select(id => id.Value<int>()))
                || !ServerProgressContract.Day(ack.Value<string>("dailyKey")) || !ServerProgressContract.Day(ack.Value<string>("weeklyKey"))
                || !DateTimeOffset.TryParseExact(ack.Value<string>("acceptedAt"), new[] { "yyyy-MM-dd'T'HH:mm:ss'Z'", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'" },
                    CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out _))
            { throw ServerProgressContract.Invalid(); }
            return ack.ToObject<BattleProgressAck>();
        }

        private static bool Integer(JToken token, long min = 0, long max = int.MaxValue) => token?.Type == JTokenType.Integer
            && long.TryParse(token.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value >= min && value <= max;
    }
}
