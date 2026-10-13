using System;

namespace Game.Core
{
    /// <summary>레벨 하나의 필요 경험치 행(PlayerLevelConfig가 만든다). Level에서 Level+1로 오르는 데 필요한 경험치</summary>
    [Serializable]
    public class PlayerLevelRow
    {
        public int Level;
        public int RequiredExp;
    }
}
