using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;
using Newtonsoft.Json.Linq;

namespace Game.Network
{
    public sealed class BattleSourceSnapshot
    {
        public string SourceRevision { get; }
        public string StagesJson { get; }
        public string MonstersJson { get; }
        public string CombatJson { get; }
        internal BattleSourceSnapshot(string revision, string stages, string monsters, string combat)
        { SourceRevision = revision; StagesJson = stages; MonstersJson = monsters; CombatJson = combat; }
    }

    /// <summary>원문 세 개와 manifest가 실제로 한 버전인지 검증한 뒤에만 공급한다. Root/Release 전환은 하지 않는다.</summary>
    public sealed class HttpBattleSourceApi
    {
        private readonly HttpApiClient _client;
        public HttpBattleSourceApi(HttpApiClient client) { _client = client; }

        public async UniTask<BattleSourceSnapshot> GetSnapshot(CancellationToken cancellationToken = default)
        {
            var body = await _client.GetJson("api/data/battle-source/snapshot", cancellationToken);
            return ServerProgressContract.Parse(() =>
            {
                if (ServerProgressContract.Read(body) is not JObject download
                    || download["manifest"] is not JObject manifest
                    || download["stagesJson"]?.Type != JTokenType.String
                    || download["monstersJson"]?.Type != JTokenType.String
                    || download["combatJson"]?.Type != JTokenType.String)
                { throw ServerProgressContract.Invalid(); }
                var stages = download.Value<string>("stagesJson");
                var monsters = download.Value<string>("monstersJson");
                var combat = download.Value<string>("combatJson");
                if (ServerProgressContract.Read(stages) is not JArray || ServerProgressContract.Read(monsters) is not JArray
                    || ServerProgressContract.Read(combat) is not JObject) { throw ServerProgressContract.Invalid(); }
                var revision = BattleSourceFingerprint.Compute(stages, monsters, combat);
                if (manifest["schemaVersion"]?.Type != JTokenType.Integer || manifest.Value<int>("schemaVersion") != 1
                    || manifest.Value<string>("sourceRevision") != revision || manifest["tables"] is not JObject hashes
                    || hashes.Properties().Count() != 3 || hashes.Value<string>("Stages") != BattleSourceFingerprint.Hash(stages)
                    || hashes.Value<string>("Monsters") != BattleSourceFingerprint.Hash(monsters)
                    || hashes.Value<string>("Combat") != BattleSourceFingerprint.Hash(combat))
                { throw ServerProgressContract.Invalid(); }
                return new BattleSourceSnapshot(revision, stages, monsters, combat);
            });
        }

        /// <summary>서버 원문을 실제 모델로 파싱한다. 로컬 전투 JSON과의 수동 동기화는 요구하지 않는다.</summary>
        public async UniTask<GameDataStore> LoadGameData(IReadOnlyDictionary<string, string> ancillaryTables,
            CancellationToken cancellationToken = default)
        {
            if (ancillaryTables == null) { throw new ArgumentNullException(nameof(ancillaryTables)); }
            var snapshot = await GetSnapshot(cancellationToken);
            var tables = ancillaryTables.ToDictionary(pair => pair.Key, pair => pair.Value);
            tables["Stages"] = snapshot.StagesJson;
            tables["Monsters"] = snapshot.MonstersJson;
            tables["Combat"] = snapshot.CombatJson;
            return new GameDataStore(tables);
        }
    }
}
