using System;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using Game.Core;
using Game.Network;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class HttpBattleSourceTests
    {
        [Test]
        public void AnyRawChangeChangesLoadedSourceFingerprint()
        {
            var data = new GameDataStore();
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
        public void DifferentServerCombatConfigBecomesActualRuntimeData()
        {
            var data = new GameDataStore();
            var combat = JObject.Parse(data.RawJson("Combat"));
            combat["BaseTimeScale"] = data.Combat.BaseTimeScale + 0.25f;
            var transport = Transport(data, data.RawJson("Stages"), combat: combat.ToString());
            var api = new HttpBattleSourceApi(new HttpApiClient(new HttpApiConfig("https://example.test"), transport));
            var ancillary = data.CopyTables();
            foreach (var name in new[] { "Stages", "Monsters", "Combat" }) { ancillary.Remove(name); }
            var loaded = api.LoadGameData(ancillary).GetAwaiter().GetResult();
            Assert.AreEqual(data.Combat.BaseTimeScale + 0.25f, loaded.Combat.BaseTimeScale);
            Assert.AreNotEqual(data.BattleSourceRevision, loaded.BattleSourceRevision);
            Assert.AreEqual(combat.ToString(), loaded.RawJson("Combat"));
        }

        [Test]
        public void BindUsesActualRevisionAndDifferentSourceRequiresServerLedger()
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
            Assert.AreEqual(2, transport.Requests.Count);
            Assert.AreEqual(HttpVerb.Get, transport.Requests.Last().Verb);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void LobbyRefresh_ReadsPublicProfileOnceAndKeepsProgress(bool serverProgress)
        {
            var data = new GameDataStore();
            var profile = new PlayerProfile(data);
            var transport = serverProgress ? Transport(data, data.RawJson("Stages")) : new FakeHttpTransport();
            var tokens = new AuthSession();
            tokens.Set("header.eyJzdWIiOiI0MiJ9.signature", null);
            var client = new HttpApiClient(new HttpApiConfig("https://example.test"), transport, tokens: tokens);
            string directory = Path.Combine(Path.GetTempPath(), "profile-refresh-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var save = new LocalSaveStore(Path.Combine(directory, "save.json"), data);
                var localAccount = new LocalAccountApi(save);
                var publicProfile = new ServerBackedAccountApi(client, serverProgress ? null : localAccount, tokens);
                IPlayerApi player;
                if (serverProgress)
                {
                    transport.Queued.Enqueue(new HttpResponseData(200, "{\"tables\":{\"PlayerLevels\":1}}"));
                    transport.Queued.Enqueue(new HttpResponseData(200, "{\"baseExp\":10,\"stepExp\":1,\"maxLevel\":50,\"version\":1}"));
                    transport.Queued.Enqueue(new HttpResponseData(200, HttpAccountProgressTests.State));
                    player = new HttpPlayerApi(client, tokens, data, new HttpBattleSourceApi(client),
                        new HttpDataVersionApi(client), publicProfile);
                }
                else
                {
                    var saved = save.Load();
                    saved.wallet.gold = 137;
                    save.Flush(saved);
                    player = new LocalPlayerApi(save);
                }
                transport.Queued.Enqueue(new HttpResponseData(200,
                    "{\"userId\":42,\"nickname\":\"서버이름\",\"avatarId\":3}"));
                var account = new AccountService(publicProfile, null, null, null, profile, profiles: publicProfile);
                var refresher = new LobbyProfileRefresher(player, profile, account, refreshPublicProfile: !serverProgress);

                refresher.Start();

                Assert.AreEqual(UniTaskStatus.Succeeded, refresher.WhenLoaded.Status);
                Assert.AreEqual("서버이름", profile.Nickname);
                Assert.AreEqual(3, profile.AvatarId);
                Assert.AreEqual("42", profile.PlayerId);
                Assert.AreEqual(serverProgress ? JObject.Parse(HttpAccountProgressTests.State).Value<int>("gold") : 137,
                    profile.Gold);
                Assert.AreEqual(1, transport.Requests.Count(r => r.Url.EndsWith("/api/user/profile", StringComparison.Ordinal)),
                    "서버 진행과 인증된 로컬 진행 모두 로비 갱신당 프로필 GET은 한 번이다");
                Assert.IsEmpty(transport.Queued);
            }
            finally { Directory.Delete(directory, true); }
        }

        private static FakeHttpTransport Transport(GameDataStore data, string stages, bool mixed = false, string combat = null)
        {
            var monsters = data.RawJson("Monsters");
            combat ??= data.RawJson("Combat");
            var transport = new FakeHttpTransport();
            var manifest = new JObject
            {
                ["schemaVersion"] = 1,
                ["sourceRevision"] = BattleSourceFingerprint.Compute(stages, monsters, combat),
                ["tables"] = new JObject { ["Stages"] = BattleSourceFingerprint.Hash(stages), ["Monsters"] = BattleSourceFingerprint.Hash(monsters), ["Combat"] = BattleSourceFingerprint.Hash(combat) },
            };
            transport.Queued.Enqueue(new HttpResponseData(200, new JObject
            {
                ["manifest"] = manifest,
                ["stagesJson"] = stages,
                ["monstersJson"] = monsters,
                ["combatJson"] = mixed ? combat + "\n" : combat,
            }.ToString()));
            return transport;
        }
    }
}
