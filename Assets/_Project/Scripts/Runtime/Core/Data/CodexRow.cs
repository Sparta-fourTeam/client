using System;

namespace Game.Core
{
    /// <summary>도감에 등록된 몬스터 한 마리. 전투에서 한 번이라도 마주치면 줄이 생기고, 등록 보상을 받으면 claimed가 켜진다</summary>
    [Serializable]
    public class CodexRow
    {
        public int monsterId;
        public bool claimed;
    }
}
