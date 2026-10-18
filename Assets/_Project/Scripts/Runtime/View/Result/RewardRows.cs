using System.Collections.Generic;
using Game.Core;

namespace Game.View
{
    /// <summary>결과 팝업과 일시정지 창이 같은 순서·같은 아이콘 규칙으로 보상 목록을 만든다</summary>
    public static class RewardRows
    {
        /// <summary>코인·EXP 칸의 ItemId. 아이템이 아니라서 Items 표에는 없다</summary>
        public const string CoinId = "currency.coin";
        public const string GemId = "currency.gem";
        public const string ExpId = "currency.exp";
        public const string BonusExpId = "currency.exp.bonus";

        /// <summary>수령 응답으로 실제 증가한 재화·아이템만 보여준다.</summary>
        public static List<ResultRewardItem> Gained(ItemIconTable icons, GameDataStore data, PlayerSnapshot before, PlayerSnapshot after)
        {
            var previous = new Dictionary<string, int>();
            foreach (var item in before.items ?? new List<ItemAmount>())
            {
                previous.TryGetValue(item.itemId, out int quantity);
                previous[item.itemId] = quantity + item.quantity;
            }

            var gained = new List<ItemAmount>();
            foreach (var item in after.items ?? new List<ItemAmount>())
            {
                previous.TryGetValue(item.itemId, out int quantity);
                if (item.quantity > quantity)
                {
                    gained.Add(new ItemAmount { itemId = item.itemId, quantity = item.quantity - quantity });
                }
            }

            int coin = System.Math.Max(0, after.gold - before.gold);
            var rows = Build(icons, data, coin, System.Math.Max(0, after.exp - before.exp), gained);
            if (coin == 0) { rows.RemoveAt(0); }
            return rows;
        }

        /// <summary>코인은 항상, EXP는 받을 때만 보여준다. 재료는 주어진 순서대로 나열하고 보석상자를 맨 끝에 둔다</summary>
        public static List<ResultRewardItem> Build(ItemIconTable icons, GameDataStore data, int coin, int exp, IEnumerable<ItemAmount> items, int bonusExp = 0, bool preview = false)
        {
            if (bonusExp < 0 || bonusExp > exp) { throw new System.ArgumentOutOfRangeException(nameof(bonusExp)); }
            int baseExp = exp - bonusExp;
            var rows = new List<ResultRewardItem> { new(icons != null ? icons.Coin : null, coin, CoinId) };
            if (baseExp > 0)
            {
                rows.Add(new ResultRewardItem(icons != null ? icons.Exp : null, baseExp, ExpId));
            }

            if (bonusExp > 0)
            {
                rows.Add(new ResultRewardItem(icons != null ? icons.Exp : null, bonusExp, BonusExpId));
            }

            var chests = new List<ResultRewardItem>();
            foreach (var item in items)
            {
                bool randomPreview = preview && RandomRewardOdds.IsRandom(item.itemId, data);
                var icon = randomPreview ? IconOf(icons, data, item.itemId) : icons != null ? icons.Get(item.itemId) : null;
                var row = new ResultRewardItem(icon, item.quantity, item.itemId, randomPreview);
                if (item.itemId == ItemIds.GemChest) { chests.Add(row); } else { rows.Add(row); }
            }

            rows.AddRange(chests);
            return rows;
        }

        public static UnityEngine.Sprite IconOf(ItemIconTable icons, GameDataStore data, string itemId)
        {
            if (icons == null)
            {
                return null;
            }

            if (RandomRewardOdds.IsRandom(itemId, data))
            {
                return icons.GetRandom(data.RewardRules.Items[itemId].Kind == "SkillMaterial"
                    ? ItemIds.RandomSkillMaterial : ItemIds.RandomEquipmentMaterial);
            }

            return icons.Get(itemId);
        }
    }
}
