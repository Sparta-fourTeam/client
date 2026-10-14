using System;

namespace Game.Core
{
    /// <summary>전투 공통 수치. 지금은 치명타율과 치명타 피해 배율이다(서버 Combat 테이블).
    /// 기획서에 기본값이 없어 Combat.json의 값은 밸런싱 전까지 쓰는 가안이다</summary>
    [Serializable]
    public class CombatConfig
    {
        /// <summary>스킬 피해가 치명타가 될 확률(0~1)</summary>
        public float CriticalChance;

        /// <summary>치명타 피해 배율(1 이상). 1.5면 피해가 1.5배다</summary>
        public float CriticalMultiplier = 1f;
    }
}
