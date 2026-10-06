using System;
using System.IO;
using Game.Core;
using Game.Network;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class LocalItemApiTests
    {
        private string _path;
        private LocalSaveStore _store;
        private GameDataStore _data;
        private LocalItemApi _api;

        [SetUp]
        public void SetUp()
        {
            _path = Path.Combine(Path.GetTempPath(), $"local-items-{Guid.NewGuid()}.json");
            _store = new LocalSaveStore(_path);
            _data = new GameDataStore();
            _api = new LocalItemApi(_store, _data);
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_path)) { File.Delete(_path); }
        }

        private static ItemAmount Book(string id, int quantity) => new() { itemId = id, quantity = quantity };

        [Test]
        public void Grant_PersistsAggregatedBooksAndPreservesCurrencies()
        {
            var save = _store.Load();
            save.wallet.gold = 123;
            save.wallet.energyStored = 7;
            _store.Flush(save);
            var snapshot = _api.Grant(new[]
            {
                Book(ItemIds.ArrowBook, 2), Book(ItemIds.WeaponBook, 4), Book(ItemIds.ArrowBook, 3)
            }, "drop-1").GetAwaiter().GetResult();

            Assert.AreEqual(123, snapshot.gold);
            Assert.AreEqual(7, snapshot.energyStored);
            Assert.AreEqual(2, snapshot.items.Count);
            Assert.AreEqual(5, snapshot.items.Find(item => item.itemId == ItemIds.ArrowBook).quantity);
            Assert.AreEqual(4, _store.Load().items.Find(item => item.itemId == ItemIds.WeaponBook).quantity);
            var profile = new PlayerProfile(_data);
            profile.Apply(new LocalPlayerApi(_store).GetMe().GetAwaiter().GetResult());
            Assert.AreEqual(5, profile.ItemQuantity(ItemIds.ArrowBook));
        }

        [Test]
        public void Grant_RetryAfterApiRecreationDoesNotPayAgainAndAcceptsEquivalentBatch()
        {
            _api.Grant(new[] { Book(ItemIds.ArrowBook, 2), Book(ItemIds.WeaponBook, 4), Book(ItemIds.ArrowBook, 3) },
                "drop-1").GetAwaiter().GetResult();
            var restarted = new LocalItemApi(new LocalSaveStore(_path), _data);
            var snapshot = restarted.Grant(new[] { Book(ItemIds.WeaponBook, 4), Book(ItemIds.ArrowBook, 5) },
                "drop-1").GetAwaiter().GetResult();

            Assert.AreEqual(5, snapshot.items.Find(item => item.itemId == ItemIds.ArrowBook).quantity);
            Assert.AreEqual(1, _store.Load().itemGrants.Count);
            restarted.Grant(new[] { Book(ItemIds.ArrowBook, 1) }, "drop-2").GetAwaiter().GetResult();
            Assert.AreEqual(6, _store.Load().items.Find(item => item.itemId == ItemIds.ArrowBook).quantity);
        }

        [Test]
        public void Grant_SameKeyWithDifferentContentsIsRejected()
        {
            _api.Grant(new[] { Book(ItemIds.ArrowBook, 2) }, "drop-1").GetAwaiter().GetResult();
            var before = File.ReadAllText(_path);
            var error = Assert.Throws<ApiException>(() => _api.Grant(new[] { Book(ItemIds.ArrowBook, 3) },
                "drop-1").GetAwaiter().GetResult());
            Assert.AreEqual("IDEMPOTENCY_CONFLICT", error.Code);
            Assert.AreEqual(before, File.ReadAllText(_path));
        }

        [TestCase("unknown")]
        [TestCase("gold")]
        [TestCase("coin")]
        public void Grant_UnknownItemOrCurrencyRejectsEntireBatch(string id)
        {
            var error = Assert.Throws<ApiException>(() => _api.Grant(new[]
                { Book(ItemIds.ArrowBook, 2), Book(id, 1) }, "drop-1").GetAwaiter().GetResult());
            Assert.AreEqual("UNKNOWN_DATA_ID", error.Code);
            Assert.IsFalse(_store.Exists());
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void Grant_RequiresPositiveQuantity(int quantity)
        {
            var error = Assert.Throws<ApiException>(() => _api.Grant(new[] { Book(ItemIds.ArrowBook, quantity) },
                "drop-1").GetAwaiter().GetResult());
            Assert.AreEqual("INVALID_REQUEST", error.Code);
            Assert.IsFalse(_store.Exists());
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void Grant_RequiresIdempotencyKey(string key)
        {
            Assert.Throws<ApiException>(() => _api.Grant(new[] { Book(ItemIds.ArrowBook, 1) }, key).GetAwaiter().GetResult());
            Assert.IsFalse(_store.Exists());
        }

        [Test]
        public void Grant_RejectsNullOrEmptyBatchAndNullRows()
        {
            Assert.Throws<ApiException>(() => _api.Grant(null, "key").GetAwaiter().GetResult());
            Assert.Throws<ApiException>(() => _api.Grant(Array.Empty<ItemAmount>(), "key").GetAwaiter().GetResult());
            Assert.Throws<ApiException>(() => _api.Grant(new ItemAmount[] { null }, "key").GetAwaiter().GetResult());
            Assert.IsFalse(_store.Exists());
        }

        [Test]
        public void Grant_OverflowDoesNotPersistPartialBatchOrConsumeKey()
        {
            _api.Grant(new[] { Book(ItemIds.WeaponBook, int.MaxValue) }, "seed").GetAwaiter().GetResult();
            var before = File.ReadAllText(_path);
            var error = Assert.Throws<ApiException>(() => _api.Grant(new[]
                { Book(ItemIds.ArrowBook, 2), Book(ItemIds.WeaponBook, 1) }, "drop-1").GetAwaiter().GetResult());
            Assert.AreEqual("ITEM_QUANTITY_OVERFLOW", error.Code);
            Assert.AreEqual(before, File.ReadAllText(_path));
            _api.Grant(new[] { Book(ItemIds.ArrowBook, 2) }, "drop-1").GetAwaiter().GetResult();
            Assert.AreEqual(2, _store.Load().items.Find(item => item.itemId == ItemIds.ArrowBook).quantity);
        }

        [Test]
        public void Grant_LoadsOldSaveAndKeepsItemKeysSeparateFromGoldLedger()
        {
            File.WriteAllText(_path, "{\"wallet\":{\"gold\":123},\"ledger\":[{\"idempotencyKey\":\"key\",\"sourceType\":\"UPGRADE\"}]}");
            var result = _api.Grant(new[] { Book(ItemIds.ArrowBook, 2) }, "key").GetAwaiter().GetResult();
            Assert.AreEqual(123, result.gold);
            Assert.AreEqual(2, result.items[0].quantity);
            Assert.AreEqual(1, _store.Load().ledger.Count);
        }
    }
}
