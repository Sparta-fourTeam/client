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
        /// <summary>onEvent/periodic: 자식이 부모의 스탯을 가져오는 규칙. 키는 스탯 이름(예: "damage"), 값은 배율이다.
        /// 시전 시점의 부모 스탯 × 배율이 자식의 값이 된다. 자식 연결을 처음 만드는 효과만 쓸 수 있다</summary>
        public System.Collections.Generic.Dictionary<string, float> inherit;
        /// <summary>0이 아니면 이 효과(스탯 효과)는 부모가 아니라 해당 ID의 자식 스킬에만 적용된다. 자식을 시전하는 선행 카드가 필요하다</summary>
        public int target;
        /// <summary>자식 스킬이 한 번의 시전에서 쏘는 수. 1보다 크면 자식의 발사 수(projectileCount)가 이 값이 되고,
        /// 발은 가장 가까운 적부터 서로 다른 적에게 나뉜다. 1이면 자식의 기본 발사 수를 쓴다</summary>
        public int count = 1;
        /// <summary>true면 자식이 방금 맞은 적을 노리지 않고 지나친다 (분열 조각처럼 맞은 적 주변의 다른 적을 노릴 때)</summary>
        public bool excludeHit;
        /// <summary>periodic: 시전 주기(초)</summary>
        public float interval;
    }
}
