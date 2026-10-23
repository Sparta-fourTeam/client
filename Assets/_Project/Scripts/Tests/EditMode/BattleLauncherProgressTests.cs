using System;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using Game.Core;
using Game.Network;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class BattleLauncherProgressTests
    {
        [Test]
        public void PolicyRejectionDoesNotClearExistingBattleOrPendingProgress()
        {
            var checkpoint = Checkpoint();
            checkpoint.Stage.PendingProgress = new BattleProgressRequest { battleId = checkpoint.BattleId, reportId = "keep" };
            var store = new Store(); store.Save(checkpoint);
            using var launcher = new BattleLauncher(new Api(), null, null, null, store, access: new DeniedAccess());
            Assert.Throws<ApiException>(() => launcher.Resume(checkpoint).GetAwaiter().GetResult());
            Assert.Throws<ApiException>(() => launcher.Launch(1).GetAwaiter().GetResult());
            Assert.AreEqual("keep", store.Load().Stage.PendingProgress.reportId);
            Assert.AreEqual(checkpoint.BattleId, store.Load().BattleId);
        }

        private sealed class DeniedAccess : IClientAccessGuard
        {
            public UniTask EnsureAllowed(ClientAccessPurpose purpose, System.Threading.CancellationToken cancellationToken = default)
            { throw new ApiException(ApiErrorKind.Transient, "MAINTENANCE"); }
        }

        [Test]
        public void LostAbandonAckRestoresIdenticalTerminalRequestAfterRestart()
        {
            var store = new Store();
            var api = new Api { LoseAbandon = true };
            var launcher = new BattleLauncher(api, null, null, null, store);
            Assert.Throws<ApiException>(() => launcher.Discard(Checkpoint()).GetAwaiter().GetResult());
            var restored = store.Load();
            Assert.IsTrue(restored.Stage.PendingProgressAbandon);
            Assert.AreEqual(api.Bodies[0], JsonConvert.SerializeObject(restored.Stage.PendingProgress));
            new BattleLauncher(api, null, null, null, store).Discard(restored).GetAwaiter().GetResult();
            Assert.AreEqual(2, api.Bodies.Count);
            Assert.AreEqual(api.Bodies[0], api.Bodies[1]);
        }

        [Test]
        public void UnacknowledgedWaveIsReplayedBeforeNewFinalCumulativeReport()
        {
            var checkpoint = Checkpoint();
            checkpoint.Stage.PendingProgress = new BattleProgressRequest
            { battleId = checkpoint.BattleId, reportId = Guid.NewGuid().ToString("D"), killedMonsters = 3 };
            var api = new Api();
            new BattleLauncher(api, null, null, null, new Store()).Discard(checkpoint).GetAwaiter().GetResult();
            CollectionAssert.AreEqual(new[] { "report", "abandon" }, api.Paths);
            Assert.AreEqual(1, checkpoint.Stage.PendingProgress.expectedRevision);
            Assert.AreEqual(7, checkpoint.Stage.PendingProgress.killedMonsters);
            Assert.IsTrue(checkpoint.Stage.PendingProgressAbandon);
        }

        [Test]
        public void NewServerSourceDoesNotRewriteStoredBattleSourceDuringDiscard()
        {
            var checkpoint = Checkpoint();
            checkpoint.Stage.ProgressSourceRevision = new string('b', 64);
            var store = new Store();
            var api = new Api();
            new BattleLauncher(api, null, null, null, store).Discard(checkpoint).GetAwaiter().GetResult();
            Assert.AreEqual(checkpoint.Stage.ProgressSourceRevision,
                store.Load().Stage.PendingProgress.sourceRevision);
        }

        [TestCase("none")]
        [TestCase("wave")]
        [TestCase("abandon")]
        public void LocalDiscardAfterSourceUpdateUsesPinnedDataAndRetainsReceipts(string pending)
        {
            var path = Path.Combine(Path.GetTempPath(), $"discard-source-{Guid.NewGuid()}.json");
            try
            {
                var original = new GameDataStore();
                var checkpoint = Checkpoint();
                checkpoint.Stage.Encountered = new List<int> { original.Stages.GetOrThrow(1).Waves[0].Spawns[0].MonsterId };
                checkpoint.Stage.ProgressSourceRevision = original.BattleSourceRevision;
                checkpoint.PinBattleData(original);
                var save = new LocalSaveStore(path);
                var state = save.Load();
                state.battles.Add(new BattleRow { battleKey = checkpoint.BattleId, stageId = 1, status = "Issued" });
                save.Flush(state);
                if (pending != "none")
                {
                    checkpoint.Stage.PendingProgress = new BattleProgressRequest
                    {
                        battleId = checkpoint.BattleId,
                        reportId = Guid.NewGuid().ToString("D"),
                        sourceRevision = original.BattleSourceRevision,
                        killedMonsters = pending == "abandon" ? 7 : 3,
                        reachedWave = 1,
                        encounteredMonsterIds = new List<int>(checkpoint.Stage.Encountered)
                    };
                    checkpoint.Stage.PendingProgressAbandon = pending == "abandon";
                    if (pending == "abandon")
                    { new LocalBattleApi(save, original).AbandonProgress(checkpoint.Stage.PendingProgress).GetAwaiter().GetResult(); }
                }
                // 실제 재시작처럼 원문/요청을 직렬화해 캐시 없이 복구한다.
                checkpoint = JsonConvert.DeserializeObject<BattleCheckpoint>(JsonConvert.SerializeObject(checkpoint));
                string reportId = checkpoint.Stage.PendingProgress?.reportId;
                var tables = original.CopyTables();
                var combat = JObject.Parse(tables["Combat"]);
                combat["BaseTimeScale"] = original.Combat.BaseTimeScale + 0.25f;
                tables["Combat"] = combat.ToString();
                var current = new GameDataStore(tables);
                new BattleLauncher(new LocalBattleApi(save, current), null, null, null, new Store())
                    .Discard(checkpoint).GetAwaiter().GetResult();
                var battle = save.Load().battles[0];
                Assert.AreEqual("Abandoned", battle.status);
                Assert.AreEqual(7, battle.reportedKills);
                Assert.AreEqual(pending == "wave" ? 2 : 1, battle.progressReceipts.Count);
                Assert.AreEqual(original.BattleSourceRevision, battle.progressReceipts[0].ack.sourceRevision);
                if (reportId != null) { Assert.AreEqual(reportId, battle.progressReceipts[0].request.reportId); }
                Assert.AreNotEqual(original.BattleSourceRevision, current.BattleSourceRevision);
            }
            finally { if (File.Exists(path)) { File.Delete(path); } }
        }

        private static BattleCheckpoint Checkpoint() => new()
        {
            BattleId = "battle",
            StageId = 1,
            Stage = new StageRuntimeState { Kills = 7, CompletedWaves = 1, ReachedWave = 2, Encountered = new List<int> { 1, 2 } },
        };

        private sealed class Store : IBattleCheckpointStore
        {
            private string _json;
            public BattleCheckpoint Load() => JsonConvert.DeserializeObject<BattleCheckpoint>(_json);
            public void Save(BattleCheckpoint checkpoint)
            {
                _json = JsonConvert.SerializeObject(checkpoint);
            }
            public void QueueSave(BattleCheckpoint checkpoint) => Save(checkpoint);
            public void Flush() { }
            public void Clear() => _json = null;
        }

        private sealed class Api : IBattleApi, IBattleProgressApi
        {
            public string SourceRevision => new string('a', 64);
            public bool LoseAbandon;
            public readonly List<string> Paths = new(), Bodies = new();
            public UniTask<StartBattleResponse> StartBattle(int stageId) => throw new NotSupportedException();
            public UniTask<SubmitResultResponse> SubmitResult(SubmitResultRequest request) => throw new NotSupportedException();
            public UniTask<BattleProgressAck> ReportProgress(BattleProgressRequest request) => Send("report", request);
            public UniTask<BattleProgressAck> AbandonProgress(BattleProgressRequest request) => Send("abandon", request);
            private UniTask<BattleProgressAck> Send(string path, BattleProgressRequest request)
            {
                Paths.Add(path); Bodies.Add(JsonConvert.SerializeObject(request));
                if (path == "abandon" && LoseAbandon)
                { LoseAbandon = false; throw new ApiException(ApiErrorKind.Transient, "TIMEOUT"); }
                return UniTask.FromResult(new BattleProgressAck { revision = request.expectedRevision + 1 });
            }
        }
    }
}
