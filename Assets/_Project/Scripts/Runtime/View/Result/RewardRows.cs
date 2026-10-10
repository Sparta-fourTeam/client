using System.Collections.Generic;
using Game.Core;

namespace Game.View
{
    /// <summary>결과 팝업과 일시정지 창이 같은 순서·같은 아이콘 규칙으로 보상 목록을 만든다</summary>
    public static class RewardRows
    {
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
        public static List<ResultRewardItem> Build(ItemIconTable icons, GameDataStore data, int coin, int exp, IEnumerable<ItemAmount> items)
        {
            var rows = new List<ResultRewardItem> { new(icons != null ? icons.Coin : null, coin) };
            if (exp > 0)
            {
                rows.Add(new ResultRewardItem(icons != null ? icons.Exp : null, exp));
            }

            var chests = new List<ResultRewardItem>();
            foreach (var item in items)
            {
                var row = new ResultRewardItem(IconOf(icons, data, item.itemId), item.quantity);
                if (item.itemId == ItemIds.GemChest) { chests.Add(row); } else { rows.Add(row); }
            }

            rows.AddRange(chests);
            return rows;
        }

        private static UnityEngine.Sprite IconOf(ItemIconTable icons, GameDataStore data, string itemId)
        {
            if (icons == null)
            {
                return null;
            }

            if (itemId == ItemIds.RandomSkillMaterial || itemId == ItemIds.RandomEquipmentMaterial)
            {
                return icons.GetRandom(itemId);
            }

            return icons.Get(data.Items.Contains(itemId) ? data.Items.GetOrThrow(itemId).IconKey : null);
        }
    }
}
