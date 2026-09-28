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
    }
}
