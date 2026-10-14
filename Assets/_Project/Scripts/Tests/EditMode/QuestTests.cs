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
