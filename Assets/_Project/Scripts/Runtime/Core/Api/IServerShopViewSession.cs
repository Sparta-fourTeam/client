using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Game.Core
{
    public enum ShopViewSubmissionStatus { Acknowledged, Unknown, Rejected }

    public sealed class ShopViewReward
    {
        public string ItemId { get; }
        public int Count { get; }
        public ShopViewReward(string itemId, int count) { ItemId = itemId; Count = count; }
    }

    public sealed class ShopViewOffer
    {
        public int Price { get; }
        public int DiscountPercent { get; }
        public IReadOnlyList<ShopViewReward> Rewards { get; }
        public ShopViewOffer(int price, int discountPercent, IReadOnlyList<ShopViewReward> rewards)
        { Price = price; DiscountPercent = discountPercent; Rewards = rewards; }
    }

    public sealed class ShopViewDay
    {
        public bool Available { get; }
        public DateTimeOffset NextResetAt { get; }
        public int GoldUsed { get; }
        public int GoldLeft { get; }
        public int GoldRerollCost { get; }
        public IReadOnlyList<int> Purchased { get; }
        public IReadOnlyList<ShopViewOffer> Offers { get; }
        public ShopViewDay(bool available, DateTimeOffset nextResetAt, int goldUsed, int goldLeft,
            int goldRerollCost, IReadOnlyList<int> purchased, IReadOnlyList<ShopViewOffer> offers)
        {
            Available = available; NextResetAt = nextResetAt; GoldUsed = goldUsed; GoldLeft = goldLeft;
            GoldRerollCost = goldRerollCost; Purchased = purchased; Offers = offers;
        }
    }

    public sealed class ShopViewAccount
    {
        public int Gold { get; }
        public int Gems { get; }
        public ShopViewDay Daily { get; }
        public ShopViewAccount(int gold, int gems, ShopViewDay daily)
        { Gold = gold; Gems = gems; Daily = daily; }
    }

    public sealed class ShopViewPack
    {
        public int Gold { get; }
        public int GemPrice { get; }
        public int DiscountPercent { get; }
        public ShopViewPack(int gold, int gemPrice, int discountPercent)
        { Gold = gold; GemPrice = gemPrice; DiscountPercent = discountPercent; }
    }

    public sealed class ShopViewCatalog
    {
        public bool Available { get; }
        public IReadOnlyList<ShopViewPack> Packs { get; }
        public ShopViewCatalog(bool available, IReadOnlyList<ShopViewPack> packs)
        { Available = available; Packs = packs; }
    }

    public sealed class ShopViewResult
    {
        public ShopViewSubmissionStatus Status { get; }
        public string ErrorCode { get; }
        public IReadOnlyList<ShopViewReward> Rewards { get; }
        public ShopViewResult(ShopViewSubmissionStatus status, string errorCode, IReadOnlyList<ShopViewReward> rewards)
        { Status = status; ErrorCode = errorCode; Rewards = rewards; }
    }

    /// <summary>서버 상점 화면의 주입 계약. HTTP 구현은 Network에만 둔다.</summary>
    public interface IServerShopViewSession
    {
        ShopViewAccount Account { get; }
        ShopViewCatalog GoldPacks { get; }
        bool IsBusy { get; }
        bool HasPending { get; }
        event Action Changed;
        UniTask<ShopViewResult> Restore();
        UniTask<ShopViewResult> BuyDaily(ShopViewDay quoted, int slot);
        UniTask<ShopViewResult> GoldReroll(ShopViewDay quoted);
        UniTask<ShopViewResult> BuyGoldPack(ShopViewCatalog quoted, int index);
        UniTask<ShopViewResult> RetryPending();
    }
}
