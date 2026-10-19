using System;
using System.IO;
using System.Text;
using Game.Core;
using Game.Network;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class ServerBattleCheckpointStoreTests
    {
        private string _savePath;

        [SetUp]
        public void SetUp() => _savePath = Path.Combine(Path.GetTempPath(), "server-checkpoint-" + Guid.NewGuid() + ".json");

        [TearDown]
        public void TearDown()
        {
            string directory = Path.GetDirectoryName(_savePath);
            foreach (var file in Directory.GetFiles(directory, Path.GetFileName(_savePath) + "*")) { File.Delete(file); }
        }

        [Test]
        public void Checkpoint_RequiresIssuedServerBattle_AndRestoresOnlyTheSameAccount()
        {
            var data = new GameDataStore();
            var tokens = new AuthSession();
            tokens.Set(Token(101), null);
            var save = new LocalSaveStore(_savePath, data);
            var state = State(data);
            using var store = new ServerBattleCheckpointStore(save, null, new PlayerProfile(data), data, tokens);
            int durable = 0;
            store.ServerBattleDurable += (battleId, stageId) => durable++;

            Assert.Throws<IOException>(() => store.Save(state));
            Assert.AreEqual(0, durable);
            store.AuthorizeServerBattle(state.BattleId, state.StageId);
            store.Clear(); // BattleLauncher가 이전 전투를 지우면 새 발급 인증도 무효화된다.
            Assert.Throws<IOException>(() => store.Save(state));
            store.AuthorizeServerBattle(state.BattleId, state.StageId);
            Assert.DoesNotThrow(() => store.Save(state)); // 로컬 Issued BattleRow는 없다.
            Assert.AreEqual(1, durable);

            tokens.Set(Token(202), null);
            Assert.IsNull(store.Load());
            Assert.Throws<IOException>(() => store.Save(state));

            tokens.Set(Token(101), null);
            var restored = store.Load();
            Assert.IsNotNull(restored);
            Assert.AreEqual(state.BattleId, restored.BattleId);
            Assert.AreEqual(2, durable, "재인증 뒤 복원한 완성본도 보류된 시작 키를 정리할 수 있습니다");
            Assert.DoesNotThrow(() => store.Save(restored));
        }

        private static BattleCheckpoint State(GameDataStore data) => new()
        {
            BattleId = Guid.NewGuid().ToString("D"),
            StageId = data.FirstStageId,
            RandomState = JsonUtility.ToJson(UnityEngine.Random.state),
            Stage = new StageRuntimeState
            {
                State = StageState.Paused,
                BeforePause = StageState.Playing,
                Speed = 1,
                WallHp = 30,
                WallMaxHp = 50,
                ProgressSourceRevision = data.BattleSourceRevision
            },
            Spawns = new SpawnRuntimeState()
        };

        private static string Token(long subject)
        {
            string payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(new JObject { ["sub"] = subject.ToString() }.ToString()))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');
            return "eyJhbGciOiJub25lIn0." + payload + ".signature";
        }
    }
}
