namespace Game.Core
{
    /// <summary>카드가 가진 효과 하나. kind는 <see cref="EffectRegistry"/>에 등록된 키(예: "damage")다.
    /// 숫자 하나로 충분한 효과는 value만 쓰고, 자식 스킬을 시전하는 효과(onEvent, periodic)는 나머지 필드를 쓴다.</summary>
    public class EffectDef
    {
        public string kind;
        public float value;

        /// <summary>onEvent: 자식 스킬을 시전할 시점 (Start, Hit, Kill, Expired, Bounce, Impact)</summary>
        public AttackEvent trigger;
        /// <summary>자식 스킬의 무기 ID. 카탈로그 검증이 존재와 순환을 확인한다</summary>
        public int skillId;
        /// <summary>시전 확률 (0 초과 1 이하)</summary>
        public float chance = 1;
        /// <summary>자식 스킬 피해 배율 (1 = 자식의 기본 피해 그대로, 0.5 = 절반). "소형" 자식 스킬을 표현한다</summary>
        public float damageScale = 1;
        /// <summary>한 번에 시전하는 횟수</summary>
        public int count = 1;
        /// <summary>periodic: 시전 주기(초)</summary>
        public float interval;
    }
}
