using System.Collections.Generic;
using Game.Core;
using Game.Network;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class LocalItemSpendTests
    {
        private static LocalSave SaveWith(string itemId, int quantity) => new()
        {
            items = new List<ItemAmount> { new() { itemId = itemId, quantity = quantity } }
        };

        [Test(Description = "재료를 소모하면 소지량이 줄고, 0이 되면 행을 지운다")]
        public void Spend_ReducesQuantity_AndRemovesEmptyRow()
        {
            var save = SaveWith(ItemIds.HatBook, 5);

            LocalItemSpend.Spend(save, ItemIds.HatBook, 2);
            Assert.AreEqual(3, LocalItemSpend.Owned(save, ItemIds.HatBook));

            LocalItemSpend.Spend(save, ItemIds.HatBook, 3);
            Assert.AreEqual(0, LocalItemSpend.Owned(save, ItemIds.HatBook));
            Assert.IsEmpty(save.items);
        }

        [Test(Description = "소지량이 모자라거나 없는 아이템이면 INSUFFICIENT_ITEM으로 거절하고 아무것도 바꾸지 않는다")]
        public void Spend_RejectsWhenInsufficient_WithoutChangingAnything()
        {
            var save = SaveWith(ItemIds.HatBook, 1);

            var notEnough = Assert.Throws<ApiException>(() => LocalItemSpend.Spend(save, ItemIds.HatBook, 2));
            var missing = Assert.Throws<ApiException>(() => LocalItemSpend.Spend(save, ItemIds.TopBook, 1));

            Assert.AreEqual("INSUFFICIENT_ITEM", notEnough.Code);
            Assert.AreEqual("INSUFFICIENT_ITEM", missing.Code);
            Assert.AreEqual(1, LocalItemSpend.Owned(save, ItemIds.HatBook));
        }

        [Test(Description = "소모량이 0 이하이거나 아이템 ID가 비어 있으면 INVALID_ITEM_COST로 거절한다")]
        public void Spend_RejectsInvalidCost()
        {
            var save = SaveWith(ItemIds.HatBook, 1);

            Assert.AreEqual("INVALID_ITEM_COST", Assert.Throws<ApiException>(() => LocalItemSpend.Spend(save, ItemIds.HatBook, 0)).Code);
            Assert.AreEqual("INVALID_ITEM_COST", Assert.Throws<ApiException>(() => LocalItemSpend.Spend(save, "", 1)).Code);
        }
    }
}
