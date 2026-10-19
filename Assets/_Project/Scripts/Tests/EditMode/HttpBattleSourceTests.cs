using System.Linq;
using Game.Core;
using Game.Network;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class HttpBattleSourceTests
    {
        [Test]
        public void LoadedSourceMatchesServerResourceFingerprintAndAnyRawChangeChangesIt()
        {
            var data = new GameDataStore();
            Assert.AreEqual("a27121ab0bab7f840408937a12f40ab5f0bee7046387155cc5129cee2ce6e913", data.BattleSourceRevision);
            Assert.AreNotEqual(data.BattleSourceRevision, BattleSourceFingerprint.Compute(data.RawJson("Stages") + "\n", data.RawJson("Monsters"), data.RawJson("Combat")));
        }

        [Test]
        public void MixedRawResponsesNeverBecomeOneVerifiedSnapshot()
        {
            var data = new GameDataStore();
            var transport = Transport(data, data.RawJson("Stages"), mixed: true);
            var api = new HttpBattleSourceApi(new HttpApiClient(new HttpApiConfig("https://example.test"), transport));
            Assert.AreEqual(HttpErrorMapper.InvalidResponse, Assert.Throws<ApiException>(() => api.GetSnapshot().GetAwaiter().GetResult()).Code);
        }

        [Test]
        public void DifferentCompleteServerSnapshotDoesNotMatchLocalLoadedData()
        {
            var data = new GameDataStore();
            var transport = Transport(data, data.RawJson("Stages") + "\n");
            var api = new HttpBattleSourceApi(new HttpApiClient(new HttpApiConfig("https://example.test"), transport));
            Assert.AreEqual("BATTLE_SOURCE_MISMATCH", Assert.Throws<ApiException>(() => api.VerifyLocal(data).GetAwaiter().GetResult()).Code);
        }

        [Test]
        public void BindUsesActualLoadedRevisionAndDifferentReportNeverDispatches()
        {
            var data = new GameDataStore();
            var transport = new FakeHttpTransport
            {
                Response = new HttpResponseData(200,
                new JObject { ["closed"] = false, ["progress"] = new JObject { ["battleId"] = "battle", ["stageId"] = 1, ["sourceRevision"] = data.BattleSourceRevision } }.ToString())
            };
            var api = new HttpBattleProgressApi(new HttpApiClient(new HttpApiConfig("https://example.test"), transport), 1, data);
            api.Bind("battle").GetAwaiter().GetResult();
            Assert.AreEqual(data.BattleSourceRevision, JObject.Parse(transport.Requests.Single().Body).Value<string>("sourceRevision"));
            Assert.AreEqual("BATTLE_SOURCE_MISMATCH", Assert.Throws<ApiException>(() => api.ReportProgress(new BattleProgressRequest
            { battleId = "battle", sourceRevision = new string('0', 64) }).GetAwaiter().GetResult()).Code);
            Assert.AreEqual(1, transport.Requests.Count);
        }

        private static FakeHttpTransport Transport(GameDataStore data, string stages, bool mixed = false)
        {
            var monsters = data.RawJson("Monsters");
            var combat = data.RawJson("Combat");
            var transport = new FakeHttpTransport();
            transport.Queued.Enqueue(new HttpResponseData(200, new JObject
            {
                ["schemaVersion"] = 1,
                ["sourceRevision"] = BattleSourceFingerprint.Compute(stages, monsters, combat),
                ["tables"] = new JObject { ["Stages"] = BattleSourceFingerprint.Hash(stages), ["Monsters"] = BattleSourceFingerprint.Hash(monsters), ["Combat"] = BattleSourceFingerprint.Hash(combat) },
            }.ToString()));
            foreach (var raw in new[] { stages, monsters, mixed ? combat + "\n" : combat })
            { transport.Queued.Enqueue(new HttpResponseData(200, raw)); }
            return transport;
        }
    }
}
