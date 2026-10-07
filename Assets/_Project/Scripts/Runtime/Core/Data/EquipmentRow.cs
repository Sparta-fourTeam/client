using System;

namespace Game.Core
{
    /// <summary>장비 하나의 강화 레벨. 강화한 적이 없는 장비는 행이 없고 레벨 0이다</summary>
    [Serializable]
    public class EquipmentRow
    {
        public string equipmentId;
        public int level;
    }
}
