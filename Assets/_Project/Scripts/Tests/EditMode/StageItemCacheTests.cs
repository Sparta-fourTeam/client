using System;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class StageItemCacheTests
    {
        [Test]
        public void Begin_DeduplicatesPossibilitiesWithoutMarkingThemAcquired()
        {
            var ids = new[] { ItemIds.ArrowBook, ItemIds.FireballBook, ItemIds.ArrowBook };
            var cache = new StageItemCache();
            cache.Begin(1, ids);
            ids[0] = ItemIds.HatBook;

            CollectionAssert.AreEqual(new[] { ItemIds.ArrowBook, ItemIds.FireballBook }, cache.ObtainableItemIds);
            Assert.IsEmpty(cache.AcquiredItems);
            Assert.AreEqual(0, cache.AcquiredQuantity(ItemIds.ArrowBook));
        }

        [TestCase(1)]
        [TestCase(2)]
        public void Begin_RestartOrTransitionClearsPreviousBattle(int nextStage)
        {
            var cache = new StageItemCache();
            cache.Begin(1, new[] { ItemIds.ArrowBook });
            cache.RecordAcquired(ItemIds.ArrowBook, 2);
            cache.Begin(nextStage, new[] { ItemIds.HatBook });

            Assert.AreEqual(nextStage, cache.StageId);
            CollectionAssert.AreEqual(new[] { ItemIds.HatBook }, cache.ObtainableItemIds);
            Assert.IsEmpty(cache.AcquiredItems);
        }

        [Test]
        public void RecordAcquired_AggregatesEachIdAndReturnsIndependentSnapshots()
        {
            var cache = new StageItemCache();
            cache.Begin(1, new[] { ItemIds.ArrowBook, ItemIds.FireballBook });
            cache.RecordAcquired(ItemIds.ArrowBook, 2);
            var before = cache.AcquiredItems;
            cache.RecordAcquired(ItemIds.ArrowBook, 3);
            cache.RecordAcquired(ItemIds.FireballBook, 7);

            Assert.AreEqual(2, before[0].quantity);
            before[0].quantity = 100;
            Assert.AreEqual(5, cache.AcquiredQuantity(ItemIds.ArrowBook));
            Assert.AreEqual(7, cache.AcquiredQuantity(ItemIds.FireballBook));
            Assert.AreEqual(2, cache.AcquiredItems.Count);
        }

        [Test]
        public void InvalidBegin_KeepsCurrentBattleIntact()
        {
            var cache = new StageItemCache();
            cache.Begin(1, new[] { ItemIds.ArrowBook });
            cache.RecordAcquired(ItemIds.ArrowBook, 2);
            Assert.Throws<ArgumentException>(() => cache.Begin(2, new[] { ItemIds.HatBook, "" }));
            Assert.AreEqual(1, cache.StageId);
            Assert.AreEqual(2, cache.AcquiredQuantity(ItemIds.ArrowBook));
        }

        [Test]
        public void RecordAcquired_RejectsInvalidQuantitiesAndOverflowWithoutChangingTotals()
        {
            var cache = new StageItemCache();
            Assert.Throws<InvalidOperationException>(() => cache.RecordAcquired(ItemIds.ArrowBook, 1));
            cache.Begin(1, new[] { ItemIds.ArrowBook });
            Assert.Throws<ArgumentOutOfRangeException>(() => cache.RecordAcquired(ItemIds.ArrowBook, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => cache.RecordAcquired(ItemIds.ArrowBook, -1));
            cache.RecordAcquired(ItemIds.ArrowBook, int.MaxValue);
            Assert.Throws<OverflowException>(() => cache.RecordAcquired(ItemIds.ArrowBook, 1));
            Assert.AreEqual(int.MaxValue, cache.AcquiredQuantity(ItemIds.ArrowBook));
        }

        [Test]
        public void SetAcquired_ReplacesTotalsAndKeepsPreviousOnInvalidInput()
        {
            var cache = new StageItemCache();
            cache.Begin(1, new[] { ItemIds.ArrowBook, ItemIds.FireballBook });
            cache.SetAcquired(new[] { new ItemAmount { itemId = ItemIds.ArrowBook, quantity = 2 } });
            cache.SetAcquired(new[]
            {
                new ItemAmount { itemId = ItemIds.ArrowBook, quantity = 5 },
                new ItemAmount { itemId = ItemIds.FireballBook, quantity = 1 },
            });

            Assert.AreEqual(5, cache.AcquiredQuantity(ItemIds.ArrowBook));
            Assert.AreEqual(1, cache.AcquiredQuantity(ItemIds.FireballBook));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                cache.SetAcquired(new[] { new ItemAmount { itemId = ItemIds.ArrowBook, quantity = 0 } }));
            Assert.AreEqual(5, cache.AcquiredQuantity(ItemIds.ArrowBook));
        }
    }
}
