using System;

namespace Game.Core
{
    /// <summary>스테이지의 스폰 간격</summary>
    [Serializable]
    public class SpawnDefinition
    {
        public float IntervalMin;
        public float IntervalMax;
        public float Cooldown;
    }
}
