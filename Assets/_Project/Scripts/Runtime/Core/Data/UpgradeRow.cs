using System;

namespace Game.Core
{
    /// <summary>구매한 업그레이드 하나의 레벨</summary>
    [Serializable]
    public class UpgradeRow
    {
        public string upgradeId;
        public int level;
    }
}
