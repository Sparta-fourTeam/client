using System;
using System.IO;
using System.Linq;
using Game.Core;
using Game.Network;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>일일·주간 퀘스트: 전투·로그인 진행이 모든 보드에 세어지고, 퀘스트·상자 보상이 두 번 나가지 않으며, 주기가 지나면 그 보드만 초기화된다</summary>
    public class QuestTests
    {
        private string _filePath;
        private LocalSaveStore _store;
        private GameDataStore _data;
        private LocalBattleApi _battle;
        private LocalQuestApi _quests;
        private QuestBoardDefinition _daily;

        [SetUp]
        public void SetUp()
        {
            _filePath = Path.Combine(Path.GetTempPath(), $"quest-test-{Guid.NewGuid()}.json");
            _data = new GameDataStore();
            _store = new LocalSaveStore(_filePath, _data);
            _battle = new LocalBattleApi(_store, _data);
            _quests = new LocalQuestApi(_store, _data);
            _daily = _data.QuestBoards.First(board => board.Period == QuestPeriod.Daily);
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_filePath)) { File.Delete(_filePath); }
        }

        private QuestDefinition QuestOf(QuestBoardDefinition board, QuestKind kind) => board.Quests.First(q => q.GetKind() == kind);

        private int CountOf(string questId) => _store.Load().quests.rows.Find(r => r.questId == questId)?.count ?? 0;

        [TestCase("2026-10-18T18:59:59Z", "20261018", "2026-10-18T19:00:00Z")]
        [TestCase("2026-10-18T19:00:00Z", "20261019", "2026-10-19T19:00:00Z")]
        [TestCase("2026-10-19T00:00:00Z", "20261019", "2026-10-19T19:00:00Z")]
        [TestCase("2026-12-31T18:59:59Z", "20261231", "2026-12-31T19:00:00Z")]
        [TestCase("2026-12-31T19:00:00Z", "20270101", "2027-01-01T19:00:00Z")]
        [Test(Description = "상점·일일 퀘스트와 타이머가 UTC 입력에서 한국시간04시를 같은 경계로 사용한다")]
        public void DailyReset_SharesKoreaBoundaryAcrossFeatures(string utcText, string day, string resetText)
        {
            var utcNow = DateTime.Parse(utcText, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AdjustToUniversal);
            var reset = DateTime.Parse(resetText, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AdjustToUniversal);

            Assert.AreEqual(day, ShopRules.DayKey(utcNow));
            Assert.AreEqual(day, QuestRules.PeriodKey(QuestPeriod.Daily, utcNow));
            Assert.AreEqual(reset, GameDay.NextResetUtc(utcNow));
            Assert.AreEqual(DateTimeKind.Utc, GameDay.NextResetUtc(utcNow).Kind);
            Assert.Greater((GameDay.NextResetUtc(utcNow) - utcNow).TotalSeconds, 0);
        }

        [TestCase("2026-10-18T18:59:59Z", "20261012")]
        [TestCase("2026-10-18T19:00:00Z", "20261019")]
        [Test(Description = "주간 퀘스트는 UTC 일요일19시인 한국시간 월요일04시에 새 주간으로 넘어간다")]
        public void WeeklyReset_UsesMondayFourAmKorea(string utcText, string expected)
        {
            var utcNow = DateTime.Parse(utcText, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AdjustToUniversal);
            Assert.AreEqual(expected, QuestRules.PeriodKey(QuestPeriod.Weekly, utcNow));
        }

        // 목표를 채운 상태를 직접 만든다 (진행 계산은 다른 테스트가 본다)
        private void Complete(QuestDefinition quest)
        {
            var save = _store.Load();
            save.quests.rows.RemoveAll(r => r.questId == quest.Id);
            save.quests.rows.Add(new QuestProgressRow { questId = quest.Id, count = quest.Target });
            _store.Flush(save);
        }

        [Test(Description = "전투 입장의 에너지 소모와 제출한 처치 수가 일일·주간 보드에 함께 쌓이고, 같은 결과를 다시 제출해도 처치 수가 두 번 세어지지 않는다")]
        public void BattleProgress_CountsOnEveryBoard_AndOnlyOncePerBattle()
        {
            var save = _store.Load();
            // 진행 중인 전투 튜토리얼은 에너지를 쓰지 않으므로 모두 마친 계정으로 시작한다
            save.tutorials = _data.Tutorials.Select(t => t.Id).ToList();
            save.wallet.energyStored = _data.Energy.Cost * 2;
            save.wallet.energyUpdatedAt = DateTime.UtcNow.ToString("O");
            _store.Flush(save);

            var start = _battle.StartBattle(1).GetAwaiter().GetResult();
            var request = new SubmitResultRequest { battleId = start.battleId, cleared = false, reachedWave = 1, killedMonsters = 7 };
            _battle.SubmitResult(request).GetAwaiter().GetResult();
            _battle.SubmitResult(request).GetAwaiter().GetResult();

            foreach (var board in _data.QuestBoards)
            {
                Assert.AreEqual(_data.Energy.Cost, CountOf(QuestOf(board, QuestKind.SpendEnergy).Id), board.Id);
                Assert.AreEqual(7, CountOf(QuestOf(board, QuestKind.KillMonsters).Id), board.Id);
            }
        }

        [Test(Description = "웨이브 누적 보고와 최종 결과가 같은 처치를 다시 더하지 않고, 이어하지 않는 포기도 중간 진행을 보존한다")]
        public void CheckpointProgressAndFinalResultCountOnlyTheCumulativeDifference()
        {
            var start = _battle.StartBattle(1).GetAwaiter().GetResult();
            int monster = _data.Stages.GetOrThrow(1).Waves[0].Spawns[0].MonsterId;
            var first = new BattleProgressRequest
            {
                battleId = start.battleId,
                reportId = Guid.NewGuid().ToString("D"),
                sourceRevision = _data.BattleSourceRevision,
                killedMonsters = 3,
                reachedWave = 1,
                encounteredMonsterIds = new System.Collections.Generic.List<int> { monster },
            };
            var ack = _battle.ReportProgress(first).GetAwaiter().GetResult();
            Assert.AreEqual(3, ack.acceptedDelta);
            Assert.AreEqual(ack.revision, _battle.ReportProgress(first).GetAwaiter().GetResult().revision);
            Assert.AreEqual(3, CountOf(QuestOf(_daily, QuestKind.KillMonsters).Id));
            _battle.SubmitResult(new SubmitResultRequest
            { battleId = start.battleId, reachedWave = 1, killedMonsters = 5 }).GetAwaiter().GetResult();
            Assert.AreEqual(5, CountOf(QuestOf(_daily, QuestKind.KillMonsters).Id));
            Assert.AreEqual(ack.revision, _battle.ReportProgress(first).GetAwaiter().GetResult().revision);
            var other = _battle.StartBattle(1).GetAwaiter().GetResult();
            var discard = new BattleProgressRequest
            {
                battleId = other.battleId,
                reportId = Guid.NewGuid().ToString("D"),
                sourceRevision = _data.BattleSourceRevision,
                killedMonsters = 4,
                reachedWave = 1,
                encounteredMonsterIds = new System.Collections.Generic.List<int> { monster },
            };
            int beforeGold = _store.Load().wallet.gold;
            var receipt = _battle.AbandonProgress(discard).GetAwaiter().GetResult();
            Assert.AreEqual(receipt.revision, _battle.AbandonProgress(discard).GetAwaiter().GetResult().revision);
            Assert.AreEqual(9, CountOf(QuestOf(_daily, QuestKind.KillMonsters).Id));
            Assert.AreEqual(beforeGold, _store.Load().wallet.gold);
            Assert.AreEqual("Abandoned", _store.Load().battles.Find(b => b.battleKey == other.battleId).status);
        }

        [Test(Description = "목표 미달이면 받을 수 없고, 받은 퀘스트는 다시 받을 수 없다. 받으면 점수가 오른다")]
        public void ClaimQuest_RequiresCompletion_AndPaysPointsOnce()
        {
            var quest = QuestOf(_daily, QuestKind.KillMonsters);
            var save = _store.Load();
            save.quests.rows.Add(new QuestProgressRow { questId = quest.Id, count = quest.Target - 1 });
            _store.Flush(save);

            var notDone = Assert.Throws<ApiException>(() => _quests.ClaimQuest(quest.Id).GetAwaiter().GetResult());
            Assert.AreEqual("NOT_COMPLETE", notDone.Code);

            Complete(quest);
            var snapshot = _quests.ClaimQuest(quest.Id).GetAwaiter().GetResult();
            var profile = new PlayerProfile(_data);
            profile.Apply(snapshot);
            Assert.AreEqual(quest.Points, profile.QuestPoints(_daily));

            var again = Assert.Throws<ApiException>(() => _quests.ClaimQuest(quest.Id).GetAwaiter().GetResult());
            Assert.AreEqual("ALREADY_CLAIMED", again.Code);
        }

        [Test(Description = "게이지가 닿지 않은 상자는 열 수 없고, 닿으면 골드와 에너지를 한 번만 준다")]
        public void ClaimChest_NeedsPoints_AndPaysOnce()
        {
            var chest = _daily.Chests[0];
            var quest = _daily.Quests.First(q => q.Points >= chest.Points);
            var unreached = Assert.Throws<ApiException>(() => _quests.ClaimQuestChest(chest.Id).GetAwaiter().GetResult());
            Assert.AreEqual("NOT_REACHED", unreached.Code);

            Complete(quest);
            _quests.ClaimQuest(quest.Id).GetAwaiter().GetResult();
            var before = _store.Load();
            int energyBefore = EnergyRule.At(before.wallet.energyStored, EnergyRule.ParseUpdatedAt(before.wallet.energyUpdatedAt), DateTime.UtcNow, _data.Energy).current;

            var snapshot = _quests.ClaimQuestChest(chest.Id).GetAwaiter().GetResult();

            Assert.AreEqual(before.wallet.gold + chest.Gold, snapshot.gold);
            Assert.AreEqual(energyBefore + chest.Energy, EnergyRule.At(snapshot.energyStored, EnergyRule.ParseUpdatedAt(snapshot.energyUpdatedAt), DateTime.UtcNow, _data.Energy).current);
            CollectionAssert.Contains(snapshot.questChests, chest.Id);
            var again = Assert.Throws<ApiException>(() => _quests.ClaimQuestChest(chest.Id).GetAwaiter().GetResult());
            Assert.AreEqual("ALREADY_CLAIMED", again.Code);
        }

        [Test(Description = "잼이 들어 있는 상자는 열 때 잼도 함께 준다")]
        public void ClaimChest_PaysGems()
        {
            var weekly = _data.QuestBoards.First(board => board.Period == QuestPeriod.Weekly);
            var chest = weekly.Chests.First(c => c.Gems > 0);
            foreach (var quest in weekly.Quests) { Complete(quest); _quests.ClaimQuest(quest.Id).GetAwaiter().GetResult(); }
            int gemsBefore = _store.Load().wallet.gems;

            var snapshot = _quests.ClaimQuestChest(chest.Id).GetAwaiter().GetResult();

            Assert.AreEqual(gemsBefore + chest.Gems, snapshot.gems);
        }

        [Test(Description = "일일 주기가 바뀌면 일일 진행과 연 상자만 비워지고 주간 진행은 남는다")]
        public void Rollover_ResetsOnlyTheExpiredBoard()
        {
            var weekly = _data.QuestBoards.First(board => board.Period == QuestPeriod.Weekly);
            var dailyQuest = QuestOf(_daily, QuestKind.KillMonsters);
            var weeklyQuest = QuestOf(weekly, QuestKind.KillMonsters);
            Complete(dailyQuest);
            Complete(weeklyQuest);
            var save = _store.Load();
            save.quests.chests.Add(_daily.Chests[0].Id);
            save.quests.dailyKey = "19990101";
            _store.Flush(save);

            var reloaded = _store.Load();

            Assert.AreEqual(0, CountOf(dailyQuest.Id));
            Assert.AreEqual(weeklyQuest.Target, CountOf(weeklyQuest.Id));
            CollectionAssert.DoesNotContain(reloaded.quests.chests, _daily.Chests[0].Id);
        }
    }
}
