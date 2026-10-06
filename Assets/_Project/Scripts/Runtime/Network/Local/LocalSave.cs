using System;
using System.Collections.Generic;
using Game.Core;

namespace Game.Network
{
    /// <summary>기기에 저장되는 세이브 데이터 전체. LocalSaveStore가 파일로 직렬화한다</summary>
    [Serializable]
    public class LocalSave
    {
        public WalletRow wallet = new();
        public List<StageProgressRow> stageProgress = new() { new StageProgressRow { stageId = 1, clearRating = 0 } };
        public List<UpgradeRow> upgrades = new();
        public List<BattleRow> battles = new();
        public List<LedgerRow> ledger = new();
        /// <summary>마법북 소유량. 기존 저장 파일에서 누락된 items는 빈 목록이다.</summary>
        public List<ItemAmount> items = new();
        public List<ItemGrantRow> itemGrants = new();
    }
}
