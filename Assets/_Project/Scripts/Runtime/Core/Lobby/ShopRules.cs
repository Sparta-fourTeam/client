using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>일일상점에 놓인 물품 한 칸. 서버와 Local이 같은 규칙으로 만들고, 화면은 이것을 그린다</summary>
    public sealed class ShopOffer
    {
        public string Reward;
        public int Count;
        /// <summary>할인을 적용한 가격(잼). 0이면 무료</summary>
        public int Price;
        public int DiscountPercent;
    }

    /// <summary>일일상점의 하루 기준과 진열 규칙. 서버와 Local이 같은 규칙을 쓰고, 화면은 같은 규칙으로 진열을 읽는다</summary>
    public static class ShopRules
    {
        /// <summary>물품 보상이 코인(골드)임을 나타내는 값</summary>
        public const string CoinReward = "currency.coin";

        /// <summary>물품 보상이 에너지(잉크펜)임을 나타내는 값</summary>
        public const string EnergyReward = "currency.energy";

        /// <summary>오늘의 키. 한국시간 매일 04시에 바뀌면 상점 상태가 초기화된다.</summary>
        public static string DayKey(DateTime utcNow) => GameDay.Key(utcNow);

        /// <summary>코인 묶음의 가격(잼). 기준 비율(잼 1개 = GemGoldRate 코인)로 바꾼 값에서 할인율을 뺀다</summary>
        public static int GoldPackPrice(ShopConfig config, GoldPackDefinition pack) =>
            (int)((long)pack.Gold * (100 - pack.DiscountPercent) / (config.GemGoldRate * 100L));

        /// <summary>오늘 남은 광고 다시 뽑기 횟수</summary>
        public static int AdLeft(ShopConfig config, ShopState state) => Math.Max(0, config.AdRerollsPerDay - state.adUsed);

        /// <summary>오늘 남은 코인 다시 뽑기 횟수</summary>
        public static int GoldLeft(ShopConfig config, ShopState state) => Math.Max(0, config.GoldRerollsPerDay - state.goldUsed);

        /// <summary>오늘 진열을 만든다. 날짜와 다시 뽑은 횟수가 같으면 언제 만들어도 같은 구성이 나온다. 같은 물품이 한 번에 겹치지 않는다</summary>
        public static List<ShopOffer> Roll(ShopConfig config, string dayKey, int rerolls)
        {
            var random = new Random(int.Parse(dayKey) + rerolls * 7919);
            var order = new int[config.Offers.Count];
            for (int i = 0; i < order.Length; i++) { order[i] = i; }
            for (int i = order.Length - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }

            var offers = new List<ShopOffer>();
            for (int slot = 0; slot < config.Slots; slot++)
            {
                var entry = config.Offers[order[slot % order.Length]];
                int discount = slot == 0 ? 100 : config.Discounts[random.Next(config.Discounts.Count)];
                offers.Add(new ShopOffer
                {
                    Reward = entry.Reward,
                    Count = entry.Count,
                    DiscountPercent = discount,
                    Price = entry.Price * (100 - discount) / 100,
                });
            }

            return offers;
        }
    }
}
