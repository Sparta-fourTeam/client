using System;
using System.IO;
using Game.Core;
using Game.Network;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class LocalRewardItemTests
    {
        private string _path;
        private LocalSaveStore _store;
        private GameDataStore _data;

        [SetUp]
        public void SetUp()
        {
            _path = Path.Combine(Path.GetTempPath(), $"local-reward-items-{Guid.NewGuid()}.json");
            _store = new LocalSaveStore(_path);
            _data = new GameDataStore();
            var save = _store.Load();
            save.wallet.energyStored = 10;
            save.wallet.energyUpdatedAt = DateTime.UtcNow.ToString("O");
            _store.Flush(save);
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_path)) { File.Delete(_path); }
        }

        private static ItemAmount Book(string id, int quantity) => new() { itemId = id, quantity = quantity };
        private StageDefinition Stage => _data.Stages.GetOrThrow(1);

        private void SetRating(int rating)
        {
            var save = _store.Load();
            save.stageProgress[0].clearRating = rating;
            _store.Flush(save);
        }

        [Test]
        public void RatingReward_PaysOnlyUnclaimedTiersAndReturnsOwnedBooksWithGold()
        {
            Stage.RatingItemRewards = new()
            {
                new() { Book(ItemIds.ArrowBook, 2) },
                new() { Book(ItemIds.ArrowBook, 3), Book(ItemIds.WeaponBook, 1) },
                new() { Book(ItemIds.ArrowBook, 4) }
            };
            SetRating(2);
            var api = new LocalStageApi(_store, _data);
            var snapshot = api.ClaimRatingReward(1).GetAwaiter().GetResult();
            Assert.AreEqual(70, snapshot.gold);
            Assert.AreEqual(5, snapshot.items.Find(item => item.itemId == ItemIds.ArrowBook).quantity);
            Assert.AreEqual(1, snapshot.items.Find(item => item.itemId == ItemIds.WeaponBook).quantity);
            var before = File.ReadAllText(_path);
            var error = Assert.Throws<ApiException>(() => api.ClaimRatingReward(1).GetAwaiter().GetResult());
            Assert.AreEqual("NO_REWARD", error.Code);
            Assert.AreEqual(before, File.ReadAllText(_path));

            SetRating(3);
            snapshot = new LocalStageApi(new LocalSaveStore(_path), _data).ClaimRatingReward(1).GetAwaiter().GetResult();
            Assert.AreEqual(170, snapshot.gold);
            Assert.AreEqual(9, snapshot.items.Find(item => item.itemId == ItemIds.ArrowBook).quantity);
            Assert.AreEqual(3, snapshot.stageProgress[0].claimedRating);
        }

        [Test]
        public void BattleReward_PersistsBooksAndReplaysOriginalResultAfterRestart()
        {
            Stage.ClearItems = new() { Book(ItemIds.ArrowBook, 2), Book(ItemIds.WeaponBook, 1), Book(ItemIds.ArrowBook, 3) };
            var api = new LocalBattleApi(_store, _data);
            var issued = api.StartBattle(1, 1).GetAwaiter().GetResult();
            var request = new SubmitResultRequest { battleId = issued.battleId, cleared = true, wallHpPercent = 100 };
            var result = api.SubmitResult(request).GetAwaiter().GetResult();
            Assert.AreEqual(100, result.rewardGold);
            Assert.AreEqual(5, result.rewardItems.Find(item => item.itemId == ItemIds.ArrowBook).quantity);
            result.rewardItems[0].quantity = 999;
            Stage.ClearItems = new() { Book(ItemIds.ArrowBook, 1000) };
            request.cleared = false;
            var replay = new LocalBattleApi(new LocalSaveStore(_path), _data).SubmitResult(request).GetAwaiter().GetResult();
            Assert.IsTrue(replay.cleared);
            Assert.AreEqual(5, replay.rewardItems.Find(item => item.itemId == ItemIds.ArrowBook).quantity);
            var snapshot = new LocalPlayerApi(_store).GetMe().GetAwaiter().GetResult();
            Assert.AreEqual(100, snapshot.gold);
            Assert.AreEqual(5, snapshot.items.Find(item => item.itemId == ItemIds.ArrowBook).quantity);
            Assert.AreEqual(1, snapshot.items.Find(item => item.itemId == ItemIds.WeaponBook).quantity);
        }

        [Test]
        public void FailedBattle_DoesNotPayClearItemsOrRemoveExistingInventory()
        {
            Stage.ClearItems = new() { Book(ItemIds.ArrowBook, 2) };
            var save = _store.Load();
            save.items.Add(Book(ItemIds.ArrowBook, 5));
            _store.Flush(save);
            var api = new LocalBattleApi(_store, _data);
            var issued = api.StartBattle(1, 1).GetAwaiter().GetResult();
            var result = api.SubmitResult(new SubmitResultRequest { battleId = issued.battleId, cleared = false })
                .GetAwaiter().GetResult();
            Assert.IsEmpty(result.rewardItems);
            Assert.AreEqual(0, result.rewardGold);
            Assert.AreEqual(5, _store.Load().items[0].quantity);
        }

        [TestCase("unknown")]
        [TestCase("gold")]
        [TestCase("coin")]
        public void RatingReward_InvalidIdDoesNotPayGoldOrAdvanceClaimedRating(string id)
        {
            Stage.RatingItemRewards = new() { new() { Book(ItemIds.ArrowBook, 2), Book(id, 1) } };
            SetRating(1);
            var before = File.ReadAllText(_path);
            var error = Assert.Throws<ApiException>(() => new LocalStageApi(_store, _data)
                .ClaimRatingReward(1).GetAwaiter().GetResult());
            Assert.AreEqual("UNKNOWN_DATA_ID", error.Code);
            Assert.AreEqual(before, File.ReadAllText(_path));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void BattleReward_InvalidQuantityDoesNotFinalizeOrPay(int quantity)
        {
            Stage.ClearItems = new() { Book(ItemIds.ArrowBook, quantity) };
            var api = new LocalBattleApi(_store, _data);
            var issued = api.StartBattle(1, 1).GetAwaiter().GetResult();
            var before = File.ReadAllText(_path);
            var error = Assert.Throws<ApiException>(() => api.SubmitResult(new SubmitResultRequest
            { battleId = issued.battleId, cleared = true }).GetAwaiter().GetResult());
            Assert.AreEqual("INVALID_ITEM_REWARD", error.Code);
            Assert.AreEqual(before, File.ReadAllText(_path));
        }

        [Test]
        public void RewardOverflow_DoesNotPersistPartialItemsOrGoldOrClaimState()
        {
            var save = _store.Load();
            save.items.Add(Book(ItemIds.WeaponBook, int.MaxValue));
            save.stageProgress[0].clearRating = 1;
            _store.Flush(save);
            Stage.RatingItemRewards = new() { new() { Book(ItemIds.ArrowBook, 2), Book(ItemIds.WeaponBook, 1) } };
            var before = File.ReadAllText(_path);
            var api = new LocalStageApi(_store, _data);
            var error = Assert.Throws<ApiException>(() => api.ClaimRatingReward(1).GetAwaiter().GetResult());
            Assert.AreEqual("ITEM_QUANTITY_OVERFLOW", error.Code);
            Assert.AreEqual(before, File.ReadAllText(_path));
            Stage.RatingItemRewards = new() { new() { Book(ItemIds.ArrowBook, 2) } };
            var snapshot = api.ClaimRatingReward(1).GetAwaiter().GetResult();
            Assert.AreEqual(2, snapshot.items.Find(item => item.itemId == ItemIds.ArrowBook).quantity);
        }

        [Test]
        public void LegacyBattleWithoutItemRewards_ReplaysGoldAndEmptyItemList()
        {
            File.WriteAllText(_path, "{\"wallet\":{\"gold\":123},\"battles\":[{\"battleKey\":\"old\",\"stageId\":1,\"status\":\"Cleared\",\"rewardGold\":100}]}");
            var response = new LocalBattleApi(_store, _data).SubmitResult(new SubmitResultRequest
            { battleId = "old", cleared = true }).GetAwaiter().GetResult();
            Assert.AreEqual(100, response.rewardGold);
            Assert.IsEmpty(response.rewardItems);
            Assert.AreEqual(123, _store.Load().wallet.gold);
        }
    }
}
