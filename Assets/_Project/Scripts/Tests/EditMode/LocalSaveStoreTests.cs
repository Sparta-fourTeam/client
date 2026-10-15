using System;
using System.IO;
using System.Linq;
using Game.Core;
using Game.Network;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public class LocalSaveStoreTests
    {
        private string _filePath;
        private string _folder;

        [SetUp]
        public void CreateTempFilePath()
        {
            _filePath = Path.Combine(Path.GetTempPath(), $"local-save-test-{Guid.NewGuid()}.json");
            _folder = Path.Combine(Path.GetTempPath(), $"local-save-isolation-{Guid.NewGuid()}");
        }

        [TearDown]
        public void DeleteSaveFile()
        {
            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
            }
            foreach (string suffix in new[] { ".battle", ".battle.bak", ".battle.tmp" })
            { if (File.Exists(_filePath + suffix)) { File.Delete(_filePath + suffix); } }
            if (Directory.Exists(_folder)) { Directory.Delete(_folder, true); }
        }

        [Test(Description = "Flush()로 저장한 지갑/업그레이드 데이터를 Load()가 그대로 복원한다")]
        public void Flush_ThenLoad_RoundTripsData()
        {
            var store = new LocalSaveStore(_filePath);
            var save = new LocalSave { wallet = new WalletRow { gold = 100, energyStored = 5, energyUpdatedAt = "2026-09-28" } };
            save.upgrades.Add(new UpgradeRow { upgradeId = "atk", level = 3 });

            store.Flush(save);
            var loaded = store.Load();

            Assert.AreEqual(100, loaded.wallet.gold);
            Assert.AreEqual(5, loaded.wallet.energyStored);
            Assert.AreEqual("2026-09-28", loaded.wallet.energyUpdatedAt);
            Assert.AreEqual(1, loaded.upgrades.Count);
            Assert.AreEqual("atk", loaded.upgrades[0].upgradeId);
            Assert.AreEqual(3, loaded.upgrades[0].level);

            save.wallet.gold = 20;
            save.upgrades.Clear();
            store.Flush(save);
            var overwritten = new LocalSaveStore(_filePath).Load();
            Assert.AreEqual(20, overwritten.wallet.gold);
            Assert.IsEmpty(overwritten.upgrades);
        }

        [Test(Description = "Load()를 여러 번 반복해도 stageProgress의 기본 시드 행이 중복되지 않는다 " +
            "(필드 기본값이 있는 리스트를 Newtonsoft가 이어붙이는 문제 회귀 테스트)")]
        public void Load_CalledRepeatedly_DoesNotDuplicateSeededStageProgress()
        {
            var store = new LocalSaveStore(_filePath);
            store.Flush(store.Load());

            store.Load();
            store.Load();
            var loaded = store.Load();

            Assert.AreEqual(1, loaded.stageProgress.Count);
            loaded.stageProgress[0].clearRating = 3;
            store.Flush(loaded);
            var restored = new LocalSaveStore(_filePath).Load();
            Assert.AreEqual(1, restored.stageProgress.Count);
            Assert.AreEqual(3, restored.stageProgress[0].clearRating);
        }

        [Test(Description = "별도 프로젝트 저장 초기화가 다른 프로젝트의 발급된 전투 기록을 지우지 않는다")]
        public void EditorProjects_ResettingOneSave_DoesNotEraseOtherBattle()
        {
            string clientPath = LocalSavePath.ForEditorProject(Path.Combine(_folder, "client", "Assets"));
            string sandboxPath = LocalSavePath.ForEditorProject(Path.Combine(_folder, "sandbox", "Assets"));
            Assert.That(clientPath, Is.Not.EqualTo(sandboxPath));
            Assert.That(LocalSavePath.ForEditorProject(Path.Combine(_folder, "client", ".", "Assets")), Is.EqualTo(clientPath));

            var client = new LocalSaveStore(clientPath);
            var sandbox = new LocalSaveStore(sandboxPath);
            var save = new LocalSave();
            save.battles.Add(new BattleRow { battleKey = "issued-client-battle", status = "Issued" });
            client.Flush(save);
            sandbox.Flush(new LocalSave());
            File.Delete(sandboxPath);
            sandbox.Flush(new LocalSave());

            Assert.That(client.Load().battles.Count, Is.EqualTo(1));
            Assert.That(client.Load().battles[0].battleKey, Is.EqualTo("issued-client-battle"));
            Assert.That(sandbox.Load().battles, Is.Empty);
        }

        [TestCase(false)]
        [TestCase(true)]
        [Description("공유 세이브의 첫 이전은 원본 파일과 기존 프로젝트 데이터를 덮어쓰지 않는다")]
        public void LegacyImport_PreservesSourceAndExistingProjectSave(bool projectSaveExists)
        {
            var legacy = new LocalSaveStore(_filePath);
            legacy.Flush(new LocalSave { exp = 321 });
            byte[] original = File.ReadAllBytes(_filePath);
            string target = LocalSavePath.ForEditorProject(Path.Combine(_folder, "client", "Assets"));
            if (projectSaveExists) { new LocalSaveStore(target).Flush(new LocalSave { exp = 654 }); }

            var project = new LocalSaveStore(target, _filePath);
            Assert.That(project.Load().exp, Is.EqualTo(projectSaveExists ? 654 : 321));
            Assert.That(File.ReadAllBytes(_filePath), Is.EqualTo(original));
            project.Flush(new LocalSave { exp = 987 });
            legacy.Flush(new LocalSave { exp = 111 });
            Assert.That(new LocalSaveStore(target, _filePath).Load().exp, Is.EqualTo(987));
        }

        [Test(Description = "프로젝트 세이브 삭제 후에는 이전 공유 세이브를 다시 가져오지 않는다")]
        public void LegacyImport_AfterProjectSaveDeleted_DoesNotRestoreLegacyAgain()
        {
            new LocalSaveStore(_filePath).Flush(new LocalSave { exp = 321 });
            string target = LocalSavePath.ForEditorProject(Path.Combine(_folder, "client", "Assets"));
            new LocalSaveStore(target, _filePath);
            File.Delete(target);

            var reset = new LocalSaveStore(target, _filePath);
            Assert.That(reset.Exists(), Is.False);
            Assert.That(reset.Load().exp, Is.Zero);
            Assert.That(File.Exists(_filePath), Is.True);
        }

        private sealed class CheckpointCredentials : IAuthCredentialStore
        {
            public LoginMethod Method { get; private set; }
            public string AccountId { get; private set; } = "checkpoint-owner";
            public bool HasCredential => AccountId != null;
            public void Save(LoginMethod method, string accountId) { Method = method; AccountId = accountId; }
            public void Clear() => AccountId = null;
        }

        private (LocalSaveStore Save, LocalBattleCheckpointStore Checkpoints, CheckpointCredentials Credentials, BattleCheckpoint State) CheckpointSetup()
        {
            var save = new LocalSaveStore(_filePath);
            var data = new GameDataStore();
            var state = new BattleCheckpoint
            {
                BattleId = "checkpoint-battle",
                StageId = data.FirstStageId,
                RandomState = JsonUtility.ToJson(UnityEngine.Random.state),
                Stage = new StageRuntimeState { State = StageState.Paused, BeforePause = StageState.Playing, Speed = 2, WallHp = 30, WallMaxHp = 50 },
                Spawns = new SpawnRuntimeState()
            };
            state.Spawns.Enemies.Add(new EnemyRuntimeState
            {
                Id = 1,
                MonsterId = data.Monsters.Values.First().Id,
                Hp = 10,
                Position = new Vector2(1.25f, -2.5f),
                Status = new EnemyStatusState { Freeze = 3.25f }
            });
            var local = save.Load();
            local.battles.Add(new BattleRow { battleKey = state.BattleId, stageId = state.StageId, status = "Issued" });
            save.Flush(local);
            var credentials = new CheckpointCredentials();
            return (save, new LocalBattleCheckpointStore(save, credentials, new PlayerProfile(data), data), credentials, state);
        }

        [Test(Description = "파일 교체 중 중단·손상에도 이전 완성본으로 돌아가며 좌표와 전투 상태를 보존한다")]
        public void BattleCheckpoint_RoundTripAndCorruptMain_RecoversPreviousCompletedWrite()
        {
            var (save, checkpoints, _, state) = CheckpointSetup();
            state.Stage.Clock = 12.5f;
            checkpoints.Save(state);
            state.Stage.Clock = 25.75f;
            checkpoints.Save(state);
            var latest = checkpoints.Load();
            Assert.That(latest.Stage.Clock, Is.EqualTo(25.75f));
            Assert.That(latest.Spawns.Enemies.Single().Position, Is.EqualTo(new Vector2(1.25f, -2.5f)));
            Assert.That(latest.Spawns.Enemies.Single().Status.Freeze, Is.EqualTo(3.25f));
            File.WriteAllText(save.BattleCheckpointPath, "null");
            File.WriteAllText(save.BattleCheckpointPath + ".tmp", "unfinished");
            Assert.That(checkpoints.Load().Stage.Clock, Is.EqualTo(12.5f));
            checkpoints.Clear();
            Assert.That(checkpoints.Load(), Is.Null);
            Assert.That(File.Exists(save.BattleCheckpointPath + ".bak"), Is.False);
            Assert.That(File.Exists(save.BattleCheckpointPath + ".tmp"), Is.False);
        }

        [TestCase(true)]
        [TestCase(false)]
        [Description("다른 계정과 이미 종료된 전투의 저장 파일로는 전투를 재개하지 않는다")]
        public void BattleCheckpoint_ChangedOwnerOrFinishedBattle_IsDiscarded(bool changedOwner)
        {
            var (save, checkpoints, credentials, state) = CheckpointSetup();
            checkpoints.Save(state);
            if (changedOwner) { credentials.Save(LoginMethod.Guest, "other-account"); }
            else
            {
                var local = save.Load(); local.battles.Single().status = "Cleared"; save.Flush(local);
            }
            Assert.That(checkpoints.Load(), Is.Null);
            Assert.That(File.Exists(save.BattleCheckpointPath), Is.False);
        }

    }
}
