using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using Game.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Game.Network
{
    /// <summary>서버가 저장한 한 슬롯. 가격과 실제 보상은 Local 추첨으로 다시 계산하지 않는다.</summary>
    public sealed class ServerShopOffer
    {
        public int Slot { get; }
        public int DiscountPercent { get; }
        public int Price { get; }
        public string OfferId { get; }
        public int Gold { get; }
        public int Energy { get; }
        public IReadOnlyDictionary<string, int> Items { get; }

        internal ServerShopOffer(int slot, int discount, int price, string offerId, int gold, int energy,
            Dictionary<string, int> items)
        {
            Slot = slot;
            DiscountPercent = discount;
            Price = price;
            OfferId = offerId;
            Gold = gold;
            Energy = energy;
            Items = new ReadOnlyDictionary<string, int>(items);
        }
    }

    /// <summary>GET /api/user/shop 및 user/me.shop의 서버 확정 진열.</summary>
    public sealed class ServerDailyShop
    {
        public bool Available { get; }
        public string DayKey { get; }
        public DateTimeOffset NextResetAt { get; }
        public int ConfigVersion { get; }
        public int RerollIndex { get; }
        public int GoldUsed { get; }
        public int GoldLeft { get; }
        public int GoldRerollCost { get; }
        public int AdUsed { get; }
        public bool AdAvailable { get; }
        public IReadOnlyList<int> Purchased { get; }
        public IReadOnlyList<ServerShopOffer> Offers { get; }

        internal ServerDailyShop(bool available, string dayKey, DateTimeOffset reset, int version, int reroll,
            int goldUsed, int goldLeft, int goldCost, int adUsed, bool adAvailable,
            List<int> purchased, List<ServerShopOffer> offers)
        {
            Available = available;
            DayKey = dayKey;
            NextResetAt = reset;
            ConfigVersion = version;
            RerollIndex = reroll;
            GoldUsed = goldUsed;
            GoldLeft = goldLeft;
            GoldRerollCost = goldCost;
            AdUsed = adUsed;
            AdAvailable = adAvailable;
            Purchased = purchased.AsReadOnly();
            Offers = offers.AsReadOnly();
        }
    }

    public sealed class ServerGoldPack
    {
        public int Index { get; }
        public int Gold { get; }
        public int DiscountPercent { get; }
        public int GemPrice { get; }

        internal ServerGoldPack(int index, int gold, int discount, int price)
        { Index = index; Gold = gold; DiscountPercent = discount; GemPrice = price; }
    }

    public sealed class ServerGoldPackCatalog
    {
        public bool Available { get; }
        public int Version { get; }
        public int GemGoldRate { get; }
        public IReadOnlyList<ServerGoldPack> Packs { get; }

        internal ServerGoldPackCatalog(bool available, int version, int rate, List<ServerGoldPack> packs)
        { Available = available; Version = version; GemGoldRate = rate; Packs = packs.AsReadOnly(); }
    }

    /// <summary>user/me의 지갑과 일일 진열을 한 응답에서 읽은 계정 복원 결과.</summary>
    public sealed class ServerShopAccount
    {
        public int Gold { get; }
        public int Gems { get; }
        public ServerDailyShop Daily { get; }
        internal ServerUserData User { get; }

        internal ServerShopAccount(int gold, int gems, ServerDailyShop daily, ServerUserData user)
        { Gold = gold; Gems = gems; Daily = daily; User = user; }
    }

    public sealed class ServerDailyShopReceipt
    {
        public string RequestId { get; }
        public string Operation { get; }
        public string DayKey { get; }
        public int RequestedReroll { get; }
        public int? Slot { get; }
        public int GoldSpent { get; }
        public int GemsSpent { get; }
        public int Gold { get; }
        public int Energy { get; }
        public IReadOnlyDictionary<string, int> Items { get; }
        public ServerDailyShop StateAfter { get; }
        public DateTimeOffset CompletedAt { get; }

        internal ServerDailyShopReceipt(string key, string operation, string day, int reroll, int? slot,
            int goldSpent, int gemsSpent, int gold, int energy, Dictionary<string, int> items,
            ServerDailyShop stateAfter, DateTimeOffset completedAt)
        {
            RequestId = key; Operation = operation; DayKey = day; RequestedReroll = reroll; Slot = slot;
            GoldSpent = goldSpent; GemsSpent = gemsSpent; Gold = gold; Energy = energy;
            Items = new ReadOnlyDictionary<string, int>(items);
            StateAfter = stateAfter; CompletedAt = completedAt;
        }
    }

    public sealed class ServerGoldPackReceipt
    {
        public string RequestId { get; }
        public int ConfigVersion { get; }
        public int Index { get; }
        public int Gold { get; }
        public int GemsSpent { get; }
        public int GemGoldRate { get; }
        public int DiscountPercent { get; }
        public DateTimeOffset CompletedAt { get; }

        internal ServerGoldPackReceipt(string key, int version, int index, int gold, int gemsSpent,
            int rate, int discount, DateTimeOffset completedAt)
        {
            RequestId = key; ConfigVersion = version; Index = index; Gold = gold;
            GemsSpent = gemsSpent; GemGoldRate = rate; DiscountPercent = discount; CompletedAt = completedAt;
        }
    }

    /// <summary>서버 응답의 필수 필드를 확인해 누락·중복·잘못된 UTC와 영수증 대상 변경을 거절한다.</summary>
    internal static class ServerShopContract
    {
        internal static ServerShopAccount Account(string json)
        {
            var body = Root(json);
            var user = ServerProgressContract.Parse(() => ServerProgressContract.ReadMe(json));
            return new ServerShopAccount(Number(body["gold"]), Number(body["gem"]),
                DailyObject(Object(body["shop"])), user);
        }

        internal static ServerDailyShop Daily(string json) => DailyObject(Root(json));

        internal static ServerGoldPackCatalog GoldPacks(string json)
        {
            var body = Root(json);
            bool available = Boolean(body["available"]);
            int version = Number(body["version"]), rate = Number(body["gemGoldRate"]);
            var rows = Array(body["packs"]);
            var packs = new List<ServerGoldPack>();
            int lastGold = 0, lastDiscount = 0;
            foreach (var row in rows)
            {
                var pack = Object(row);
                int index = Number(pack["index"]), gold = Number(pack["gold"]);
                int discount = Number(pack["discountPercent"], 0, 99);
                int price = Number(pack["gemPrice"]);
                if (index != packs.Count || gold <= lastGold || discount < lastDiscount) { throw Invalid(); }
                packs.Add(new ServerGoldPack(index, gold, discount, price));
                lastGold = gold; lastDiscount = discount;
            }
            if (available ? version < 1 || rate < 1 || packs.Count == 0 : version != 0 || rate != 0 || packs.Count != 0)
            { throw Invalid(); }
            return new ServerGoldPackCatalog(available, version, rate, packs);
        }

        internal static ServerDailyShopReceipt DailyReceipt(string json, ShopExchangeRequest request)
        {
            var body = Root(json);
            string operation = Text(body["operation"]), day = Text(body["dayKey"]);
            int reroll = Number(body["requestedReroll"]);
            int? slot = body["slot"]?.Type == JTokenType.Null ? null : Number(body["slot"]);
            int goldSpent = Number(body["goldSpent"]), gemsSpent = Number(body["gemsSpent"]);
            int gold = Number(body["gold"]), energy = Number(body["energy"]);
            var items = Items(body["items"]);
            var state = DailyObject(Object(body["stateAfter"]));
            var completed = Utc(body["completedAt"]);
            if (Text(body["requestId"]) != request.RequestId || operation != request.Operation
                || day != request.DayKey || reroll != request.RerollIndex || slot != request.Slot
                || state.DayKey != day || !state.Available
                || (operation == "BUY" && (goldSpent != 0 || state.RerollIndex != reroll || !state.Purchased.Contains(slot.Value)))
                || (operation == "GOLD_REROLL" && (gemsSpent != 0 || gold != 0 || energy != 0
                    || items.Count != 0 || state.RerollIndex != reroll + 1)))
            { throw Invalid(); }
            return new ServerDailyShopReceipt(request.RequestId, operation, day, reroll, slot,
                goldSpent, gemsSpent, gold, energy, items, state, completed);
        }

        internal static ServerGoldPackReceipt GoldPackReceipt(string json, ShopExchangeRequest request)
        {
            var body = Root(json);
            int version = Number(body["configVersion"]), index = Number(body["index"]);
            int gold = Number(body["gold"]), spent = Number(body["gemsSpent"]);
            int rate = Number(body["gemGoldRate"]), discount = Number(body["discountPercent"], 0, 99);
            var completed = Utc(body["completedAt"]);
            if (Text(body["requestId"]) != request.RequestId || version != request.ConfigVersion
                || index != request.Index || gold <= 0 || rate <= 0) { throw Invalid(); }
            return new ServerGoldPackReceipt(request.RequestId, version, index, gold, spent, rate, discount, completed);
        }

        private static ServerDailyShop DailyObject(JObject body)
        {
            bool available = Boolean(body["available"]), adAvailable = Boolean(body["adAvailable"]);
            string day = Text(body["dayKey"]);
            if (!DateTime.TryParseExact(day, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            { throw Invalid(); }
            var reset = Utc(body["nextResetAt"]);
            int version = Number(body["configVersion"]), reroll = Number(body["rerollIndex"]);
            int goldUsed = Number(body["goldUsed"]), goldLeft = Number(body["goldLeft"]);
            int goldCost = Number(body["goldRerollCost"]), adUsed = Number(body["adUsed"]);
            var offers = new List<ServerShopOffer>();
            foreach (var row in Array(body["offers"]))
            {
                var stock = Object(row);
                var offer = Object(stock["offer"]);
                int slot = Number(stock["slot"]), discount = Number(stock["discountPercent"], 0, 100);
                int price = Number(stock["price"]);
                if (slot != offers.Count) { throw Invalid(); }
                offers.Add(new ServerShopOffer(slot, discount, price, Text(offer["id"]),
                    Number(offer["gold"]), Number(offer["energy"]), Items(offer["items"])));
            }
            var purchased = new List<int>();
            foreach (var row in Array(body["purchased"]))
            {
                int slot = Number(row);
                if (slot >= offers.Count || purchased.Contains(slot)) { throw Invalid(); }
                purchased.Add(slot);
            }
            if (available ? version < 1 || offers.Count == 0 : version != 0 || offers.Count != 0 || purchased.Count != 0)
            { throw Invalid(); }
            return new ServerDailyShop(available, day, reset, version, reroll, goldUsed, goldLeft,
                goldCost, adUsed, adAvailable, purchased, offers);
        }

        private static JObject Root(string json)
        {
            try
            {
                using var reader = new JsonTextReader(new StringReader(json ?? string.Empty))
                { DateParseHandling = DateParseHandling.None };
                var result = JObject.Load(reader,
                    new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if (reader.Read()) { throw Invalid(); }
                return result;
            }
            catch (JsonException) { throw Invalid(); }
        }

        private static JObject Object(JToken token) => token as JObject ?? throw Invalid();
        private static JArray Array(JToken token) => token as JArray ?? throw Invalid();
        private static bool Boolean(JToken token) => token?.Type == JTokenType.Boolean ? token.Value<bool>() : throw Invalid();
        private static string Text(JToken token) => token?.Type == JTokenType.String
            && !string.IsNullOrWhiteSpace(token.Value<string>()) ? token.Value<string>() : throw Invalid();

        private static int Number(JToken token, int min = 0, int max = int.MaxValue)
        {
            if (token?.Type != JTokenType.Integer || !long.TryParse(token.ToString(Formatting.None),
                NumberStyles.Integer, CultureInfo.InvariantCulture, out long value) || value < min || value > max)
            { throw Invalid(); }
            return (int)value;
        }

        private static Dictionary<string, int> Items(JToken token)
        {
            var result = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var item in Object(token).Properties())
            {
                if (string.IsNullOrWhiteSpace(item.Name)) { throw Invalid(); }
                result.Add(item.Name, Number(item.Value, 1));
            }
            return result;
        }

        private static DateTimeOffset Utc(JToken token)
        {
            string value = Text(token);
            // Java Instant may serialize nanoseconds; DateTimeOffset stores at most seven fractional digits.
            int dot = value.IndexOf('.');
            if (dot >= 0 && value.EndsWith("Z", StringComparison.Ordinal))
            {
                int digits = value.Length - dot - 2;
                if (digits > 7 && digits <= 9 && value.Skip(dot + 1).Take(digits).All(char.IsDigit))
                { value = value.Substring(0, dot + 8) + "Z"; }
            }
            if (!DateTimeOffset.TryParseExact(value,
                new[] { "yyyy-MM-dd'T'HH:mm:ss'Z'", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'" },
                CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var result)) { throw Invalid(); }
            return result;
        }

        private static ApiException Invalid() => new(ApiErrorKind.Rejected, HttpErrorMapper.InvalidResponse);
    }
}
