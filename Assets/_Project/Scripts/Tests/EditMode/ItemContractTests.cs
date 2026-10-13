using System;
using System.IO;
using Game.Core;
using Game.Network;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class ItemContractTests
    {

        [Test]
        public void Profile_SeparatesWalletFromBooks_AndReplacesPreviousInventory()
        {
            var profile = new PlayerProfile(new GameDataStore());
            Assert.AreEqual(0, profile.ItemQuantity("book.example"));
            profile.Apply(new PlayerSnapshot
            {
                gold = 100,
                items = new()
                {
                    new ItemAmount { itemId = ItemIds.ArrowBook, quantity = 2 },
                    new ItemAmount { itemId = ItemIds.FireballBook, quantity = 7 },
                    new ItemAmount { itemId = ItemIds.HatBook, quantity = 3 },
                    new ItemAmount { itemId = ItemIds.RingBook, quantity = 5 }
                }
            });
            Assert.AreEqual(100, profile.Gold);
            Assert.AreEqual(0, profile.ItemQuantity("gold"));
            Assert.AreEqual(2, profile.ItemQuantity("book.arrow"));
            Assert.AreEqual(7, profile.ItemQuantity("book.fireball"));
            Assert.AreEqual(3, profile.ItemQuantity("book.hat"));
            Assert.AreEqual(5, profile.ItemQuantity("book.ring"));
            Assert.AreEqual(0, profile.ItemQuantity("unknown"));
            profile.Apply(new PlayerSnapshot { gold = 50 });
            Assert.AreEqual(50, profile.Gold);
            Assert.AreEqual(0, profile.ItemQuantity("book.arrow"));
            Assert.AreEqual(0, profile.ItemQuantity("book.fireball"));
            Assert.AreEqual(0, profile.ItemQuantity("book.hat"));
            Assert.AreEqual(0, profile.ItemQuantity("book.ring"));
        }

        [Test]
        public void LocalInventory_LoadsLegacySave_AndRoundTripsIntoPlayerSnapshot()
        {
            var path = Path.Combine(Path.GetTempPath(), $"item-contract-{Guid.NewGuid()}.json");
            try
            {
                File.WriteAllText(path, "{\"wallet\":{\"gold\":123,\"energyStored\":5,\"energyUpdatedAt\":\"2026-10-06T00:00:00Z\"}}");
                var store = new LocalSaveStore(path);
                var save = store.Load();
                Assert.IsEmpty(save.items);
                Assert.AreEqual(123, save.wallet.gold);
                save.items.Add(new ItemAmount { itemId = "book.example", quantity = 4 });
                store.Flush(save);
                var snapshot = new LocalPlayerApi(store).GetMe().GetAwaiter().GetResult();
                Assert.AreEqual(123, snapshot.gold);
                Assert.AreEqual(5, snapshot.energyStored);
                Assert.AreEqual("2026-10-06T00:00:00Z", snapshot.energyUpdatedAt);
                Assert.AreEqual(1, snapshot.items.Count);
                Assert.AreEqual("book.example", snapshot.items[0].itemId);
                Assert.AreEqual(4, snapshot.items[0].quantity);
            }
            finally
            {
                if (File.Exists(path)) { File.Delete(path); }
            }
        }
    }
}
