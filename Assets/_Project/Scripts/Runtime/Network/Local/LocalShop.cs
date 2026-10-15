using System;
using System.Collections.Generic;
using Game.Core;

namespace Game.Network
{
    /// <summary>LocalSave에 저장되는 일일상점 상태. 날이 바뀌면 LocalSaveStore가 불러올 때 비운다</summary>
    [Serializable]
    public class ShopSave
    {
        public string dayKey;
        public ShopState state = new();
    }

    /// <summary>일일상점 다시 뽑기와 구매의 Local 구현. 서버 전환 후에는 서버가 이 역할을 대신한다. 저장은 호출한 API가 한 번에 한다</summary>
    internal static class LocalShop
    {
        /// <summary>날이 바뀌었으면 상점 상태를 비운다. 저장 파일을 읽을 때마다 부른다</summary>
        public static void Roll(LocalSave save, DateTime localNow)
        {
            save.shop ??= new ShopSave();
            save.shop.state ??= new ShopState();
            save.shop.state.purchased ??= new List<int>();
            string key = ShopRules.DayKey(localNow);
            if (save.shop.dayKey == key) { return; }

            save.shop.dayKey = key;
            save.shop.state = new ShopState();
        }

        public static void Reroll(LocalSave save, GameDataStore data, ShopRerollKind kind, string idempotencyKey)
        {
            var state = save.shop.state;
            var config = data.Shop;
            if (kind == ShopRerollKind.Ad)
            {
                if (ShopRules.AdLeft(config, state) <= 0) { throw new ApiException(ApiErrorKind.Rejected, "NO_REROLL_LEFT"); }
                state.adUsed++;
                state.purchased.Clear();
                return;
            }

            if (ShopRules.GoldLeft(config, state) <= 0) { throw new ApiException(ApiErrorKind.Rejected, "NO_REROLL_LEFT"); }
            if (save.wallet.gold < config.GoldRerollCost) { throw new ApiException(ApiErrorKind.Rejected, "INSUFFICIENT_GOLD"); }

            save.wallet.gold -= config.GoldRerollCost;
            save.ledger.Add(new LedgerRow { idempotencyKey = idempotencyKey, amount = -config.GoldRerollCost, sourceType = "SHOP_REROLL", sourceId = save.shop.dayKey });
            state.goldUsed++;
            state.purchased.Clear();
        }

        public static void Buy(LocalSave save, GameDataStore data, int slot, string idempotencyKey, DateTime utcNow)
        {
            var state = save.shop.state;
            var offers = ShopRules.Roll(data.Shop, save.shop.dayKey, state.adUsed + state.goldUsed);
            if (slot < 0 || slot >= offers.Count) { throw new ApiException(ApiErrorKind.Rejected, "INVALID_SLOT"); }
            if (state.purchased.Contains(slot)) { throw new ApiException(ApiErrorKind.Rejected, "SOLD_OUT"); }

            var offer = offers[slot];
            if (save.wallet.gems < offer.Price) { throw new ApiException(ApiErrorKind.Rejected, "INSUFFICIENT_GEMS"); }

            // 보상 계산이 실패하면 예외로 끝나고 아직 아무것도 바꾸지 않았으므로 저장하지 않는다
            switch (offer.Reward)
            {
                case ShopRules.CoinReward:
                    save.wallet.gold = AddChecked(save.wallet.gold, offer.Count);
                    break;
                case ShopRules.EnergyReward:
                    var (current, _) = EnergyRule.At(save.wallet.energyStored, EnergyRule.ParseUpdatedAt(save.wallet.energyUpdatedAt), utcNow, data.Energy);
                    save.wallet.energyStored = AddChecked(current, offer.Count);
                    save.wallet.energyUpdatedAt = utcNow.ToString("O");
                    break;
                default:
                    LocalItemRewards.Apply(save, new[] { new ItemAmount { itemId = offer.Reward, quantity = offer.Count } }, data);
                    break;
            }

            save.wallet.gems -= offer.Price;
            save.ledger.Add(new LedgerRow { idempotencyKey = idempotencyKey, amount = -offer.Price, sourceType = "SHOP_BUY", sourceId = $"{save.shop.dayKey}:{slot}" });
            state.purchased.Add(slot);
        }

        public static void BuyGoldPack(LocalSave save, GameDataStore data, int index, string idempotencyKey)
        {
            var packs = data.Shop.GoldPacks;
            if (index < 0 || index >= packs.Count) { throw new ApiException(ApiErrorKind.Rejected, "INVALID_PACK"); }

            var pack = packs[index];
            int price = ShopRules.GoldPackPrice(data.Shop, pack);
            if (save.wallet.gems < price) { throw new ApiException(ApiErrorKind.Rejected, "INSUFFICIENT_GEMS"); }

            save.wallet.gold = AddChecked(save.wallet.gold, pack.Gold);
            save.wallet.gems -= price;
            save.ledger.Add(new LedgerRow { idempotencyKey = idempotencyKey, amount = -price, sourceType = "SHOP_GOLD_PACK", sourceId = index.ToString() });
        }

        private static int AddChecked(int current, int added)
        {
            try { return checked(current + added); }
            catch (OverflowException) { throw new ApiException(ApiErrorKind.Rejected, "REWARD_QUANTITY_OVERFLOW"); }
        }
    }
}
