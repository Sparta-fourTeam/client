using System;
using System.IO;
using Game.Core;
using Game.Network;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>일일상점 다시 뽑기: 코인은 한 번에 비용만큼만 빠지고, 거절되면 아무것도 바뀌지 않으며, 날이 바뀌면 횟수가 돌아온다</summary>
    public class ShopTests
    {
        private string _filePath;
        private LocalSaveStore _store;
        private GameDataStore _data;
        private LocalShopApi _shop;

        [SetUp]
        public void SetUp()
        {
            _filePath = Path.Combine(Path.GetTempPath(), $"shop-test-{Guid.NewGuid()}.json");
            _data = new GameDataStore();
            _store = new LocalSaveStore(_filePath, _data);
            _shop = new LocalShopApi(_store, _data);
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_filePath)) { File.Delete(_filePath); }
        }

        private void SetGold(int gold)
        {
            var save = _store.Load();
            save.wallet.gold = gold;
            _store.Flush(save);
        }

        [Test(Description = "코인으로 다시 뽑으면 비용만큼만 빠지고 횟수가 오르며, 같은 요청을 다시 보내도 두 번 빠지지 않는다")]
        public void GoldReroll_ChargesOnce_AndCountsOnce()
        {
            SetGold(_data.Shop.GoldRerollCost * 2);

            var snapshot = _shop.Reroll(ShopRerollKind.Gold, "key-1").GetAwaiter().GetResult();
            _shop.Reroll(ShopRerollKind.Gold, "key-1").GetAwaiter().GetResult();

            Assert.AreEqual(_data.Shop.GoldRerollCost, snapshot.gold);
            Assert.AreEqual(1, snapshot.shop.goldUsed);
            Assert.AreEqual(_data.Shop.GoldRerollCost, _store.Load().wallet.gold);
        }

        [Test(Description = "코인이 모자라거나 오늘 횟수를 다 쓴 요청은 거절되고, 코인과 횟수가 그대로다")]
        public void Reroll_Rejected_ChangesNothing()
        {
            SetGold(_data.Shop.GoldRerollCost - 1);
            var poor = Assert.Throws<ApiException>(() => _shop.Reroll(ShopRerollKind.Gold, "poor").GetAwaiter().GetResult());
            Assert.AreEqual("INSUFFICIENT_GOLD", poor.Code);

            SetGold(_data.Shop.GoldRerollCost * (_data.Shop.GoldRerollsPerDay + 1));
            for (int i = 0; i < _data.Shop.GoldRerollsPerDay; i++) { _shop.Reroll(ShopRerollKind.Gold, $"k{i}").GetAwaiter().GetResult(); }
            int goldBefore = _store.Load().wallet.gold;

            var spent = Assert.Throws<ApiException>(() => _shop.Reroll(ShopRerollKind.Gold, "extra").GetAwaiter().GetResult());

            Assert.AreEqual("NO_REROLL_LEFT", spent.Code);
            Assert.AreEqual(goldBefore, _store.Load().wallet.gold);
            Assert.AreEqual(_data.Shop.GoldRerollsPerDay, _store.Load().shop.state.goldUsed);
        }

        private void SetGems(int gems)
        {
            var save = _store.Load();
            save.wallet.gems = gems;
            _store.Flush(save);
        }

        [Test(Description = "물품을 사면 잼이 가격만큼만 빠지고 보상이 지급되며, 산 칸은 다시 살 수 없고 같은 요청을 다시 보내도 두 번 처리되지 않는다")]
        public void Buy_ChargesGems_PaysReward_AndSellsOnce()
        {
            SetGems(100000);
            var save = _store.Load();
            var offer = ShopRules.Roll(_data.Shop, save.shop.dayKey, 0)[1];
            int goldBefore = save.wallet.gold;
            int itemBefore = save.items.Find(i => i.itemId == offer.Reward)?.quantity ?? 0;

            var snapshot = _shop.Buy(1, "buy-1").GetAwaiter().GetResult();
            _shop.Buy(1, "buy-1").GetAwaiter().GetResult();

            Assert.AreEqual(100000 - offer.Price, snapshot.gems);
            CollectionAssert.Contains(snapshot.shop.purchased, 1);
            if (offer.Reward == ShopRules.CoinReward) { Assert.AreEqual(goldBefore + offer.Count, snapshot.gold); }
            else if (offer.Reward != ShopRules.EnergyReward) { Assert.AreEqual(itemBefore + offer.Count, snapshot.items.Find(i => i.itemId == offer.Reward).quantity); }
            var again = Assert.Throws<ApiException>(() => _shop.Buy(1, "buy-2").GetAwaiter().GetResult());
            Assert.AreEqual("SOLD_OUT", again.Code);
        }

        [Test(Description = "잼이 모자라면 거절되고 잼과 산 칸이 그대로이며, 다시 뽑으면 산 칸 기록이 비워진다")]
        public void Buy_Rejected_ChangesNothing_AndRerollClearsPurchases()
        {
            var offer = ShopRules.Roll(_data.Shop, _store.Load().shop.dayKey, 0)[1];
            SetGems(Math.Max(0, offer.Price - 1));
            var poor = Assert.Throws<ApiException>(() => _shop.Buy(1, "poor").GetAwaiter().GetResult());
            Assert.AreEqual("INSUFFICIENT_GEMS", poor.Code);
            Assert.AreEqual(0, _store.Load().shop.state.purchased.Count);

            SetGems(offer.Price);
            _shop.Buy(1, "ok").GetAwaiter().GetResult();
            var snapshot = _shop.Reroll(ShopRerollKind.Ad, "ad").GetAwaiter().GetResult();

            Assert.AreEqual(0, snapshot.shop.purchased.Count);
        }

        [Test(Description = "코인 묶음을 사면 잼이 가격만큼만 빠지고 코인이 늘며, 잼이 모자라면 거절되고 아무것도 바뀌지 않는다. 같은 요청을 다시 보내도 두 번 처리되지 않는다")]
        public void BuyGoldPack_ChargesGems_PaysGold_AndRejectsWhenPoor()
        {
            var pack = _data.Shop.GoldPacks[^1];
            int price = ShopRules.GoldPackPrice(_data.Shop, pack);
            SetGems(price - 1);
            int goldBefore = _store.Load().wallet.gold;
            var poor = Assert.Throws<ApiException>(() => _shop.BuyGoldPack(_data.Shop.GoldPacks.Count - 1, "poor").GetAwaiter().GetResult());
            Assert.AreEqual("INSUFFICIENT_GEMS", poor.Code);
            Assert.AreEqual(goldBefore, _store.Load().wallet.gold);

            SetGems(price + 7);
            var snapshot = _shop.BuyGoldPack(_data.Shop.GoldPacks.Count - 1, "pack-1").GetAwaiter().GetResult();
            _shop.BuyGoldPack(_data.Shop.GoldPacks.Count - 1, "pack-1").GetAwaiter().GetResult();

            Assert.AreEqual(7, snapshot.gems);
            Assert.AreEqual(goldBefore + pack.Gold, snapshot.gold);
            Assert.AreEqual(7, _store.Load().wallet.gems);
        }

        [Test(Description = "날이 바뀌면 다시 뽑은 횟수가 비워진다")]
        public void Rollover_ResetsRerollCounts()
        {
            var save = _store.Load();
            save.shop.state.adUsed = _data.Shop.AdRerollsPerDay;
            save.shop.state.goldUsed = _data.Shop.GoldRerollsPerDay;
            save.shop.dayKey = "19990101";
            _store.Flush(save);

            var reloaded = _store.Load();

            Assert.AreEqual(0, reloaded.shop.state.adUsed);
            Assert.AreEqual(0, reloaded.shop.state.goldUsed);
        }
    }
}
