using System;
using System.IO;
using System.Linq;
using Game.Core;
using Game.Network;
using NUnit.Framework;

namespace Game.Tests
{
    public class LocalTutorialApiTests
    {
        private string _filePath;
        private LocalSaveStore _store;
        private LocalTutorialApi _api;
        private GameDataStore _data;

        [SetUp]
        public void SetUp()
        {
            _filePath = Path.Combine(Path.GetTempPath(), $"local-tutorial-test-{Guid.NewGuid()}.json");
            _store = new LocalSaveStore(_filePath);
            _data = new GameDataStore();
            _api = new LocalTutorialApi(_store, _data);
        }

        // 데이터의 첫 전투 튜토리얼 (처음 시작하면 들어가는 전투)
        private TutorialDefinition Intro => _data.Tutorials.First(t => t.IsBattle);

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
            }
        }

        [Test(Description = "도입 대사를 봤다는 기록은 조건 없이 저장되고, 없는 튜토리얼의 기록은 거절한다")]
        public void CompleteDialogue_SavesWithoutConditions_AndRejectsUnknownTutorial()
        {
            _store.Flush(_store.Load());
            string seenId = TutorialProgress.DialogueId(Intro.Id);

            _api.Complete(seenId).GetAwaiter().GetResult();

            Assert.That(_store.Load().tutorials, Is.EquivalentTo(new[] { seenId }));
            var ex = Assert.Throws<ApiException>(() => _api.Complete(TutorialProgress.DialogueId("missing")).GetAwaiter().GetResult());
            Assert.AreEqual("UNKNOWN_TUTORIAL", ex.Code);
        }

        [Test(Description = "전투 튜토리얼의 스테이지를 깨기 전에는 완료를 거절하고 아무것도 주지 않는다")]
        public void CompleteIntro_BeforeClear_IsRejectedWithoutChanges()
        {
            _store.Flush(_store.Load());

            var ex = Assert.Throws<ApiException>(() => _api.Complete(Intro.Id).GetAwaiter().GetResult());

            Assert.AreEqual("NOT_READY", ex.Code);
            var save = _store.Load();
            Assert.That(save.tutorials, Is.Empty);
            Assert.That(save.items, Is.Empty);
        }

        [Test(Description = "전투 튜토리얼을 깨면 선언된 보상대로 전투 경험치와 상관없이 정한 레벨의 경험치 0이 되고 강화 재료를 한 번만 받는다")]
        public void CompleteIntro_AfterClear_ReachesLevelTwoAndGrantsFirstLogUpgradeOnce()
        {
            var save = _store.Load();
            save.stageProgress.Find(p => p.stageId == Intro.Complete.StageId).clearRating = 1;
            var setLevel = Intro.Rewards.Single(r => r.Kind == TutorialReward.SetLevel);
            var material = Intro.Rewards.Single(r => r.Kind == TutorialReward.UpgradeMaterial);
            // 별 3개 보너스처럼 정한 레벨을 넘긴 경험치
            for (int level = 1; level < setLevel.Level; level++) { save.exp += _data.PlayerLevels.RequiredToNext(level); }
            save.exp += 15;
            _store.Flush(save);
            var upgrade = _data.Upgrades.GetOrThrow(material.UpgradeId);

            var first = _api.Complete(Intro.Id).GetAwaiter().GetResult();
            var second = _api.Complete(Intro.Id).GetAwaiter().GetResult();

            Assert.AreEqual(setLevel.Level, _data.PlayerLevels.At(first.snapshot.exp).level);
            Assert.AreEqual(0, _data.PlayerLevels.At(first.snapshot.exp).expIntoLevel);
            Assert.That(first.rewardItems, Has.Count.EqualTo(1));
            Assert.AreEqual(upgrade.MaterialItemId, first.rewardItems[0].itemId);
            Assert.AreEqual(upgrade.MaterialAt(material.Level), first.rewardItems[0].quantity);
            Assert.That(first.snapshot.tutorials, Does.Contain(Intro.Id));
            Assert.That(second.rewardItems, Is.Empty);
            Assert.AreEqual(upgrade.MaterialAt(material.Level), second.snapshot.items.Find(i => i.itemId == upgrade.MaterialItemId).quantity);
        }

        [Test(Description = "전투 튜토리얼을 깨면 안내할 강화의 코인 비용만큼 코인을 한 번만 받아, 가진 코인과 상관없이 첫 강화를 할 수 있다")]
        public void CompleteIntro_AfterClear_GrantsFirstUpgradeCostOnce()
        {
            var save = _store.Load();
            save.stageProgress.Find(p => p.stageId == Intro.Complete.StageId).clearRating = 1;
            save.wallet.gold = 7;
            _store.Flush(save);
            var cost = Intro.Rewards.Single(r => r.Kind == TutorialReward.UpgradeCost);
            int expected = _data.Upgrades.GetOrThrow(cost.UpgradeId).CostAt(cost.Level);

            var first = _api.Complete(Intro.Id).GetAwaiter().GetResult();
            var second = _api.Complete(Intro.Id).GetAwaiter().GetResult();

            Assert.AreEqual(expected, first.rewardGold);
            Assert.AreEqual(7 + expected, first.snapshot.gold);
            Assert.AreEqual(0, second.rewardGold);
            Assert.AreEqual(7 + expected, second.snapshot.gold);
        }

        [Test]
        public void EquipmentIntro_RequiresStageClear_GrantsUpgradeOnce_AndPersistsGuideCompletion()
        {
            var milestone = _data.Tutorials.Single(t => t.Kind == TutorialDefinition.Milestone);
            var guide = _data.Tutorials.Single(t => t.IsGuide && t.Start?.Kind == TutorialCondition.FeatureUnlocked
                && _data.FeatureUnlocks.RequiredStage(t.Start.Feature) == milestone.Complete.StageId);
            var upgrade = _data.Upgrades.GetOrThrow(guide.Complete.UpgradeId);
            var save = _store.Load();
            save.tutorials.Add(milestone.After);
            save.tutorials.Add("log_upgrade");
            save.wallet.gold = 0;
            _store.Flush(save);

            var denied = Assert.Throws<ApiException>(() => _api.Complete(milestone.Id).GetAwaiter().GetResult());
            Assert.That(denied.Code, Is.EqualTo("NOT_READY"));
            Assert.That(_store.Load().wallet.gold, Is.Zero);
            Assert.That(_store.Load().items, Is.Empty);

            save.stageProgress.Add(new StageProgressRow { stageId = milestone.Complete.StageId, clearRating = 1 });
            _store.Flush(save);
            var granted = _api.Complete(milestone.Id).GetAwaiter().GetResult();
            var duplicate = _api.Complete(milestone.Id).GetAwaiter().GetResult();
            Assert.That(granted.rewardGold, Is.EqualTo(upgrade.CostAt(guide.Complete.Level)));
            Assert.That(granted.rewardItems.Single().itemId, Is.EqualTo(upgrade.MaterialItemId));
            Assert.That(granted.rewardItems.Single().quantity, Is.EqualTo(upgrade.MaterialAt(guide.Complete.Level)));
            Assert.That(duplicate.rewardGold, Is.Zero);
            Assert.That(duplicate.rewardItems, Is.Empty);

            Assert.That(TutorialProgress.ActiveGuide(_data.Tutorials, TutorialFacts.From(Profile(granted.snapshot))).Id,
                Is.EqualTo(guide.Id));
            var purchased = new LocalUpgradeApi(_store, _data).Purchase(upgrade.UpgradeId, "equipment-tutorial")
                .GetAwaiter().GetResult();
            Assert.That(purchased.gold, Is.Zero);
            Assert.That(purchased.items.Find(item => item.itemId == upgrade.MaterialItemId)?.quantity ?? 0, Is.Zero);
            Assert.That(purchased.upgrades.Single(row => row.upgradeId == upgrade.UpgradeId).level, Is.EqualTo(guide.Complete.Level));
            _api.Complete(guide.Id).GetAwaiter().GetResult();
            var reloaded = new LocalSaveStore(_filePath, _data).Load();
            Assert.That(reloaded.tutorials, Does.Contain(guide.Id));
            Assert.That(reloaded.tutorials.Count(id => id == milestone.Id), Is.EqualTo(1));
        }

        private PlayerProfile Profile(PlayerSnapshot snapshot)
        {
            var profile = new PlayerProfile(_data);
            profile.Apply(snapshot);
            return profile;
        }

        [Test(Description = "튜토리얼이 생기기 전 저장 파일은 플레이 기록이 있으면 튜토리얼을 마친 것으로 읽는다")]
        public void Load_SaveWithoutTutorials_TreatsPlayedAccountAsDone()
        {
            var store = new LocalSaveStore(_filePath, _data);
            File.WriteAllText(_filePath, "{\"exp\":30}");
            Assert.That(store.Load().tutorials, Is.EquivalentTo(_data.Tutorials.Select(t => t.Id)));

            File.WriteAllText(_filePath, "{\"exp\":0}");
            Assert.That(store.Load().tutorials, Is.Empty);
        }
    }
}
