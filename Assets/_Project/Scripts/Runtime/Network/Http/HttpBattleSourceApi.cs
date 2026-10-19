using System;
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
            var meta = await _client.GetJson("api/data/battle-source", cancellationToken);
            var stages = await _client.GetJson("api/data/stages", cancellationToken);
            var monsters = await _client.GetJson("api/data/monsters", cancellationToken);
            var combat = await _client.GetJson("api/data/combat", cancellationToken);
            return ServerProgressContract.Parse(() =>
            {
                if (ServerProgressContract.Read(stages) is not JArray || ServerProgressContract.Read(monsters) is not JArray
                    || ServerProgressContract.Read(combat) is not JObject) { throw ServerProgressContract.Invalid(); }
                var revision = BattleSourceFingerprint.Compute(stages, monsters, combat);
                if (ServerProgressContract.Read(meta) is not JObject manifest
                    || manifest["schemaVersion"]?.Type != JTokenType.Integer || manifest.Value<int>("schemaVersion") != 1
                    || manifest.Value<string>("sourceRevision") != revision || manifest["tables"] is not JObject hashes
                    || hashes.Properties().Count() != 3 || hashes.Value<string>("Stages") != BattleSourceFingerprint.Hash(stages)
                    || hashes.Value<string>("Monsters") != BattleSourceFingerprint.Hash(monsters)
                    || hashes.Value<string>("Combat") != BattleSourceFingerprint.Hash(combat))
                { throw ServerProgressContract.Invalid(); }
                return new BattleSourceSnapshot(revision, stages, monsters, combat);
            });
        }

        public async UniTask<string> VerifyLocal(GameDataStore data, CancellationToken cancellationToken = default)
        {
            if (data == null) { throw new ArgumentNullException(nameof(data)); }
            var snapshot = await GetSnapshot(cancellationToken);
            if (snapshot.SourceRevision != data.BattleSourceRevision)
            { throw new ApiException(ApiErrorKind.Rejected, "BATTLE_SOURCE_MISMATCH"); }
            return snapshot.SourceRevision;
        }
    }
}
