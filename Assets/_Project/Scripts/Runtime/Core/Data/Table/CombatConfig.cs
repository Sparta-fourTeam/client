using System;

namespace Game.Core
{
    /// <summary>전투 공통 수치와 기본 진행 속도(서버 Combat 테이블).
    /// 기획서에 기본값이 없어 Combat.json의 값은 밸런싱 전까지 쓰는 가안이다</summary>
    [Serializable]
    public class CombatConfig
    {
        /// <summary>화면의 1배속에 대응하는 Unity 시간 배율. 이전 데이터는 1을 사용한다.</summary>
        public float BaseTimeScale = 1f;

        public void Validate()
        {
            if (BaseTimeScale <= 0f || float.IsNaN(BaseTimeScale) || float.IsInfinity(BaseTimeScale))
            {
                throw new InvalidOperationException("Combat: BaseTimeScale은 유한한 양수여야 합니다.");
            }
        }

        /// <summary>스킬 피해가 치명타가 될 확률(0~1)</summary>
        public float CriticalChance;

        /// <summary>치명타 피해 배율(1 이상). 1.5면 피해가 1.5배다</summary>
        public float CriticalMultiplier = 1f;
    }
}
