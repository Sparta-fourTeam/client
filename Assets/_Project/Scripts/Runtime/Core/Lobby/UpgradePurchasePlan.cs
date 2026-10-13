namespace Game.Core
{
    public readonly struct UpgradePurchasePlan
    {
        public readonly string UpgradeId, MaterialItemId;
        public readonly int StartLevel, Count, CoinCost, MaterialCost;
        public int EndLevel => StartLevel + Count;
        public UpgradePurchasePlan(string id, string material, int start, int count, int coins, int materials)
        { UpgradeId = id; MaterialItemId = material; StartLevel = start; Count = count; CoinCost = coins; MaterialCost = materials; }

        public static UpgradePurchasePlan Create(PlayerProfile profile, GameDataStore data, string id, bool all)
        {
            var def = data.Upgrades.GetOrThrow(id);
            int start = profile.UpgradeLevel(id), count = 0, coins = 0, materials = 0;
            if (profile.Level >= data.UnlockLevelOf(def))
            {
                for (int level = start + 1; level <= def.MaxLevel; level++)
                {
                    int coin = def.CostAt(level), material = def.UsesMaterial ? def.MaterialAt(level) : 0;
                    if (coin > profile.Gold - coins || material > profile.ItemQuantity(def.MaterialItemId) - materials)
                    {
                        break;
                    }

                    coins += coin; materials += material; count++;
                    if (!all)
                    {
                        break;
                    }
                }
            }

            return new UpgradePurchasePlan(id, def.MaterialItemId, start, count, coins, materials);
        }

        public bool SameQuote(UpgradePurchasePlan other) => UpgradeId == other.UpgradeId && MaterialItemId == other.MaterialItemId
            && StartLevel == other.StartLevel && Count == other.Count && CoinCost == other.CoinCost && MaterialCost == other.MaterialCost;
    }
}
