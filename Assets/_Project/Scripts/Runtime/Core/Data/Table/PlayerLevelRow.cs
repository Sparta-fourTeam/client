using System;

namespace Game.Core
{
    /// <summary>PlayerLevels.json의 한 행. Level에서 Level+1로 오르는 데 필요한 경험치</summary>
    [Serializable]
    public class PlayerLevelRow
    {
        public int Level;
        public int RequiredExp;
    }
}
