using System;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using Game.Core;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class BattleLauncherProgressTests
    {
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
        public void FailedDurableWritePreventsAbandonDispatch()
        {
            var api = new Api();
            var launcher = new BattleLauncher(api, null, null, null, new Store { Fail = true });
            var error = Assert.Throws<ApiException>(() => launcher.Discard(Checkpoint()).GetAwaiter().GetResult());
            Assert.AreEqual(ApiErrorKind.Transient, error.Kind);
            Assert.IsEmpty(api.Paths);
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
            public bool Fail;
            public BattleCheckpoint Load() => JsonConvert.DeserializeObject<BattleCheckpoint>(_json);
            public void Save(BattleCheckpoint checkpoint)
            {
                if (Fail) { throw new IOException("fixture storage failure"); }
                _json = JsonConvert.SerializeObject(checkpoint);
            }
            public void QueueSave(BattleCheckpoint checkpoint) => Save(checkpoint);
            public void Flush() { }
            public void Clear() => _json = null;
        }

        private sealed class Api : IBattleApi, IBattleProgressApi
        {
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
