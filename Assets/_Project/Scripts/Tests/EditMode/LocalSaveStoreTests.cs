using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Game.Core;
using Game.Network;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests
{
    public class LocalSaveStoreTests
    {
        private string _filePath;
        private string _folder;
        private readonly List<LocalBattleCheckpointStore> _checkpointStores = new();

        [SetUp]
        public void CreateTempFilePath()
        {
            _filePath = Path.Combine(Path.GetTempPath(), $"local-save-test-{Guid.NewGuid()}.json");
            _folder = Path.Combine(Path.GetTempPath(), $"local-save-isolation-{Guid.NewGuid()}");
        }

        [TearDown]
        public void DeleteSaveFile()
        {
            foreach (var checkpoints in _checkpointStores) { checkpoints.Dispose(); }
            _checkpointStores.Clear();
            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
            }
            foreach (string suffix in new[] { ".battle", ".battle.bak", ".battle.tmp" })
            { if (File.Exists(_filePath + suffix)) { File.Delete(_filePath + suffix); } }
            if (Directory.Exists(_folder)) { Directory.Delete(_folder, true); }
        }

        [Test(Description = "새 세이브의 기본 이름·공개 ID는 최초 읽기 때 저장되어 재시작해도 바뀌지 않는다")]
        public void NewIdentity_IsNamelessAndPersistsWithoutOtherWrites()
        {
            var first = new LocalSaveStore(_filePath).Load();
            var restored = new LocalSaveStore(_filePath).Load();
            Assert.That(first.nickname, Does.Match(@"\ANameless_[1-9][0-9]{5}\z"));
            Assert.That(Guid.TryParseExact(first.playerId, "N", out _), Is.True);
            Assert.That(restored.playerId, Is.EqualTo(first.playerId));
            Assert.That(restored.nickname, Is.EqualTo(first.nickname));
        }

        [Test(Description = "구 세이브에는 공개 ID만 보충하고 기존 이름·진행은 보존한다")]
        public void LegacyIdentity_BackfillsIdWithoutRenamingOrResettingProgress()
        {
            var store = new LocalSaveStore(_filePath);
            store.Flush(new LocalSave { nickname = "Ninja_123456", exp = 123 });
            var migrated = store.Load();
            var restored = new LocalSaveStore(_filePath).Load();
            Assert.That(restored.nickname, Is.EqualTo("Ninja_123456"));
            Assert.That(restored.exp, Is.EqualTo(123));
            Assert.That(restored.playerId, Is.Not.Empty.And.EqualTo(migrated.playerId));
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
            var checkpoints = new LocalBattleCheckpointStore(save, credentials, new PlayerProfile(data), data);
            _checkpointStores.Add(checkpoints);
            return (save, checkpoints, credentials, state);
        }

        [Test]
        public void BattleSourceUpdatePreservesCheckpointOutboxAndOriginalRuntimeData()
        {
            var (save, checkpoints, credentials, state) = CheckpointSetup();
            var original = new GameDataStore();
            state.Stage.ProgressSourceRevision = original.BattleSourceRevision;
            state.Stage.PendingProgress = new BattleProgressRequest
            { battleId = state.BattleId, reportId = Guid.NewGuid().ToString("D"), sourceRevision = original.BattleSourceRevision };
            state.Stage.PendingProgressAbandon = true;
            checkpoints.Save(state);
            var tables = original.CopyTables();
            var combat = Newtonsoft.Json.Linq.JObject.Parse(tables["Combat"]);
            combat["BaseTimeScale"] = original.Combat.BaseTimeScale + 0.25f;
            tables["Combat"] = combat.ToString();
            var current = new GameDataStore(tables);
            var reopened = new LocalBattleCheckpointStore(save, credentials, new PlayerProfile(current), current);
            _checkpointStores.Add(reopened);
            var restored = reopened.Load();
            Assert.IsNotNull(restored);
            Assert.AreEqual(state.Stage.PendingProgress.reportId, restored.Stage.PendingProgress.reportId);
            Assert.IsTrue(restored.Stage.PendingProgressAbandon);
            Assert.AreEqual(original.BattleSourceRevision, restored.RestoreBattleData(current).BattleSourceRevision);
            Assert.AreEqual(original.Combat.BaseTimeScale, restored.RestoreBattleData(current).Combat.BaseTimeScale);
            Assert.IsTrue(File.Exists(save.BattleCheckpointPath));
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

        private static BattleCheckpoint CheckpointAt(BattleCheckpoint template, float clock) => new()
        {
            BattleId = template.BattleId,
            StageId = template.StageId,
            RandomState = template.RandomState,
            Stage = new StageRuntimeState
            {
                State = StageState.Paused,
                BeforePause = StageState.Playing,
                Speed = 2,
                Clock = clock,
                WallHp = 30,
                WallMaxHp = 50
            },
            Spawns = template.Spawns
        };

        [Test(Description = "저장 요청이 몰려도 마지막 독립 스냅샷이 남고 종료 시 대기 중인 저장까지 완료한다")]
        public void BattleCheckpoint_QueuedWrites_DisposeDrainsLatestAndRejectsFurtherWrites()
        {
            var (_, checkpoints, _, template) = CheckpointSetup();
            for (int i = 0; i < 100; i++) { checkpoints.QueueSave(CheckpointAt(template, i)); }
            checkpoints.Dispose();
            Assert.That(checkpoints.Load().Stage.Clock, Is.EqualTo(99));
            checkpoints.QueueSave(CheckpointAt(template, 1000));
            Assert.That(checkpoints.Load().Stage.Clock, Is.EqualTo(99));
        }

        [Test(Description = "저장 중 삭제하면 이전 요청이 파일을 되살리지 않으며 이후 새 전투 저장은 정상 동작한다")]
        public void BattleCheckpoint_ClearWhileQueued_OldRequestsCannotResurrectFiles()
        {
            var (save, checkpoints, _, template) = CheckpointSetup();
            for (int i = 0; i < 20; i++)
            {
                checkpoints.QueueSave(CheckpointAt(template, i));
                checkpoints.Clear();
                checkpoints.Flush();
                foreach (string suffix in new[] { "", ".bak", ".tmp" })
                { Assert.That(File.Exists(save.BattleCheckpointPath + suffix), Is.False, "삭제 전 작업이 파일을 다시 만들면 안 된다"); }
            }
            checkpoints.QueueSave(CheckpointAt(template, 42));
            checkpoints.Flush();
            Assert.That(checkpoints.Load().Stage.Clock, Is.EqualTo(42));
        }

        [Test(Description = "백그라운드 파일 쓰기가 실패해도 기존 완성본을 보존하고 다음 저장을 계속 처리한다")]
        public void BattleCheckpoint_BackgroundWriteFailure_PreservesPreviousAndAllowsRetry()
        {
            var (save, checkpoints, _, template) = CheckpointSetup();
            checkpoints.Save(CheckpointAt(template, 10));
            string blocked = save.BattleCheckpointPath + ".tmp";
            Directory.CreateDirectory(blocked);
            try
            {
                LogAssert.Expect(LogType.Warning, new Regex(@"\[BattleCheckpoint\] 전투 저장 실패:.*"));
                checkpoints.QueueSave(CheckpointAt(template, 20));
                checkpoints.Flush();
            }
            finally { Directory.Delete(blocked); }
            Assert.That(checkpoints.Load().Stage.Clock, Is.EqualTo(10));
            checkpoints.QueueSave(CheckpointAt(template, 30));
            checkpoints.Flush();
            Assert.That(checkpoints.Load().Stage.Clock, Is.EqualTo(30));
        }

        [Test(Description = "직렬화 버퍼를 재사용해도 긴 한글/이스케이프 문자열의 잔여 바이트가 다음 저장에 섞이지 않고 스탯을 보존한다")]
        public void BattleCheckpoint_ReusedJsonBuffer_LongThenShort_RoundTripsExactPayload()
        {
            var (_, checkpoints, _, state) = CheckpointSetup();
            var values = new[] { 0f, -0.25f, 1.234567f, float.Epsilon, float.MaxValue };
            state.Skills.Add(new SkillRuntimeState { Config = new SkillConfigState { Stats = values } });
            string text = new string('끽', 10000) + "\"\\\n😀";
            state.Stage.BuildLog.Add(text);
            checkpoints.Save(state);
            Assert.That(checkpoints.Load().Stage.BuildLog.Single(), Is.EqualTo(text));
            Assert.That(checkpoints.Load().Skills.Single().Config.Stats, Is.EqualTo(values));
            state.Stage.BuildLog.Clear();
            state.Stage.Clock = 27.5f;
            values[2] = 99.75f;
            checkpoints.Save(state);
            var restored = checkpoints.Load();
            Assert.That(restored.Stage.Clock, Is.EqualTo(27.5f));
            Assert.That(restored.Stage.BuildLog, Is.Empty);
            Assert.That(restored.Skills.Single().Config.Stats, Is.EqualTo(values));
        }

    }
}
