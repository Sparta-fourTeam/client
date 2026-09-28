using System;
using System.Collections.Generic;

namespace Game.Network
{
    [Serializable]
    public class WalletRow
    {
        public int gold;
        public int energyStored;
        public string energyUpdatedAt;
    }

    [Serializable]
    public class StageProgressRow
    {
        public int stageId;
        public int clearRating; // 0 = not cleared, 1 = cleared, 2 = cleared with hp 50%, 3 = cleared with hp 100%
    }

    [Serializable]
    public class UpgradeRow
    {
        public string upgradeId;
        public int level;
    }

    [Serializable]
    public class BattleRow
    {
        public string battleKey;
        public int stageId;
        public int seed;
        public string status;
        public int rewardGold;
    }

    [Serializable]
    public class LedgerRow
    {
        public string idempotencyKey; //  ex)"battle-abc-reward"
        public int amount;              // ex) 100
        public string sourceType;   // ex) "BATTLE_REWARD"
        public string sourceId;     // ex) "battle-abc"
    }

    [Serializable]
    public class LocalSave
    {
        public WalletRow wallet = new();
        public List<StageProgressRow> stageProgress = new() { new StageProgressRow { stageId = 1, clearRating = 0 } };
        public List<UpgradeRow> upgrades = new();
        public List<BattleRow> battles = new();
        public List<LedgerRow> ledger = new();
    }
}
