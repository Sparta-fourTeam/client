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
        private string _openedBattle;
        public HttpBattleProgressApi(HttpApiClient client, int stageId) { _client = client; _stageId = stageId; }

        public UniTask<BattleProgressAck> ReportProgress(BattleProgressRequest request) => Send(request, false);
        public UniTask<BattleProgressAck> AbandonProgress(BattleProgressRequest request) => Send(request, true);

        private async UniTask<BattleProgressAck> Send(BattleProgressRequest request, bool abandon)
        {
            if (request == null) { throw new ArgumentNullException(nameof(request)); }
            string json;
            var path = abandon ? "api/battle/progress/abandon" : "api/battle/progress";
            try { json = await _client.PostJson(path, request); }
            catch (ApiException error) when (error.Code == "HTTP_404" && _openedBattle != request.battleId)
            {
                // 원장이 없다는 최초 404만 open 뒤 재전송한다. 닫힌 전투의 기존 보고는 open 없이 원래 ACK를 읽는다.
                var openedJson = await _client.PostJson("api/battle/progress/open", new { battleId = request.battleId, stageId = _stageId });
                ServerProgressContract.Parse(() =>
                {
                    if (ServerProgressContract.Read(openedJson) is not JObject opened || opened["closed"]?.Type != JTokenType.Boolean
                        || opened.Value<bool>("closed") || opened["progress"]?.Value<string>("battleId") != request.battleId
                        || !Integer(opened["progress"]?["stageId"], _stageId, _stageId)) { throw ServerProgressContract.Invalid(); }
                    return true;
                });
                _openedBattle = request.battleId;
                json = await _client.PostJson(path, request);
            }
            return ServerProgressContract.Parse(() => ParseAck(json, request));
        }

        private BattleProgressAck ParseAck(string json, BattleProgressRequest request)
        {
            if (ServerProgressContract.Read(json) is not JObject ack || ack.Value<string>("battleId") != request.battleId
                || !Integer(ack["stageId"], _stageId, _stageId) || !Integer(ack["policyVersion"], 1)
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
