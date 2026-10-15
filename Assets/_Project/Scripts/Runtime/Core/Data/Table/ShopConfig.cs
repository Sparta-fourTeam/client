using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>일일상점 설정. 하루에 다시 뽑을 수 있는 횟수와 코인 비용, 진열 칸 수, 진열될 수 있는 물품 목록</summary>
    [Serializable]
    public class ShopConfig
    {
        /// <summary>광고를 보고 다시 뽑을 수 있는 하루 횟수</summary>
        public int AdRerollsPerDay;

        /// <summary>코인을 내고 다시 뽑을 수 있는 하루 횟수</summary>
        public int GoldRerollsPerDay;

        /// <summary>코인으로 다시 뽑을 때 한 번에 드는 코인</summary>
        public int GoldRerollCost;

        /// <summary>한 번에 진열하는 칸 수. 칸의 순서가 화면의 순서이고, 첫 칸은 매일 무료다</summary>
        public int Slots;

        /// <summary>진열할 때 물품에 붙을 수 있는 할인율(%). 첫 칸은 항상 100%다</summary>
        public List<int> Discounts = new();

        /// <summary>진열될 수 있는 물품. 정가는 잼이다</summary>
        public List<ShopOfferDefinition> Offers = new();

        /// <summary>잼 1개로 살 수 있는 코인(할인 전). 코인 상점의 기준 비율이다</summary>
        public int GemGoldRate;

        /// <summary>코인 상점에서 잼으로 살 수 있는 코인 묶음. 코인이 많은 묶음일수록 할인율이 같거나 높다</summary>
        public List<GoldPackDefinition> GoldPacks = new();

        /// <summary>잼 상점에서 현금으로 살 수 있는 잼 묶음. 값이 큰 묶음일수록 잼은 늘고 보너스 비율은 같거나 높다</summary>
        public List<GemPackDefinition> GemPacks = new();
    }

    /// <summary>잼 상점의 잼 묶음 하나. Gems는 보너스를 포함해 받는 잼이고, BonusPercent는 카드에 붙는 보너스 표시(%)다</summary>
    [Serializable]
    public class GemPackDefinition
    {
        public int Gems;
        public int BonusPercent;
        /// <summary>결제 금액(원)</summary>
        public int PriceKrw;
    }

    /// <summary>코인 상점의 코인 묶음 하나. 가격(잼)은 코인을 기준 비율로 바꾼 값에서 할인율을 뺀 것이다(ShopRules.GoldPackPrice)</summary>
    [Serializable]
    public class GoldPackDefinition
    {
        public int Gold;
        public int DiscountPercent;
    }

    /// <summary>일일상점에 진열될 수 있는 물품 한 종류. Reward는 코인·에너지(ShopRules) 또는 Items의 아이템 ID다</summary>
    [Serializable]
    public class ShopOfferDefinition
    {
        public string Reward;
        public int Count;
        /// <summary>할인 전 가격(잼)</summary>
        public int Price;
    }
}
