namespace Game.Core
{
    /// <summary>카드가 가진 효과 하나. kind는 <see cref="EffectRegistry"/>에 등록된 키(예: "damage")다.
    /// 숫자 하나로 충분한 효과는 value만 쓰고, 자식 스킬을 시전하는 효과(onEvent, periodic)는 나머지 필드를 쓴다.</summary>
    public class EffectDef
    {
        public string kind;
        public float value;

        /// <summary>kind가 "form"일 때 변형 이름(<see cref="SkillForm"/>, 예: "TriangleIce"). 데이터에는 숫자 대신 이름을 적는다.
        /// 읽을 때 value로 바뀌어 이후 코드는 value만 본다. 없는 이름이면 value가 NaN이 되어 카탈로그 검증이 카드 ID와 함께 거부한다</summary>
        public string form;

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
        /// <summary>0이 아니면 이 효과는 부모가 아니라 해당 ID의 스킬에만 적용된다. 그 스킬이 이 부모 아래 어느 단계에서 시전되든 적용되며,
        /// 아직 시전되지 않았어도 보관했다가 시전될 때 적용한다. 스탯 효과, onEvent/periodic(그 스킬에 반응 붙이기), inherit를 쓸 수 있다</summary>
        public int target;
        /// <summary>자식 스킬이 한 번의 시전에서 쏘는 수. 1보다 크면 자식의 발사 수(projectileCount)가 이 값이 되고,
        /// 발은 가장 가까운 적부터 서로 다른 적에게 나뉜다. 1이면 자식의 기본 발사 수를 쓴다</summary>
        public int count = 1;
        /// <summary>onEvent/periodic: 지정한 형태(WeaponForm 이름, 예: "Default")일 때만 이 반응이 붙는다. 형태가 바뀌면 이 반응은 빠진다.
        /// 삼각 얼음창처럼 형태가 바뀌면 기존 분열 대신 다른 분열을 쓸 때 쓴다</summary>
        public string onlyForm;
        /// <summary>true면 자식이 방금 맞은 적을 노리지 않고 지나친다 (분열 조각처럼 맞은 적 주변의 다른 적을 노릴 때)</summary>
        public bool excludeHit;
        /// <summary>periodic: 시전 주기(초)</summary>
        public float interval;

        [System.Runtime.Serialization.OnDeserialized]
        private void ResolveForm(System.Runtime.Serialization.StreamingContext context)
        {
            if (kind != "form" || string.IsNullOrEmpty(form)) { return; }
            value = System.Enum.TryParse<SkillForm>(form, true, out var parsed) && System.Enum.IsDefined(typeof(SkillForm), parsed)
                ? (int)parsed
                : float.NaN;
        }
    }
}
