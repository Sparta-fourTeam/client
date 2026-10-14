namespace Game.Core.Combat
{
    /// <summary>적에게 주는 피해 한 번. 피해량과 함께 어느 스킬의 어떤 속성·시전 형태인지, 직접 적중 피해(충격)인지를 담는다.
    /// 적의 피해 계산(속성 저항, 투사체 차단 등)이 이 값을 본다</summary>
    public readonly struct DamageInfo
    {
        public int Amount { get; }

        /// <summary>피해를 낸 스킬의 ID. 0이면 스킬 밖(상태이상 지속 피해 등)이라 속성과 시전 형태는 의미가 없다</summary>
        public int SkillId { get; }
        public Element Element { get; }
        public CastType CastType { get; }

        /// <summary>스킬에서 나온 피해인가. 상태이상 지속 피해 같은 스킬 밖의 피해는 속성 계산을 받지 않는다</summary>
        public bool IsFromSkill => SkillId != 0;

        /// <summary>공격이 대상에 닿아 주는 직접 피해. 폭발, 추가 번개, 장판 피해 같은 부가 반응의 피해는 아니다</summary>
        public bool IsImpact { get; }

        /// <summary>치명타로 늘어난 피해인가. 치명타는 스킬 피해에만 나오고, 상태이상 지속 피해는 치명타가 없다</summary>
        public bool IsCritical { get; }

        public DamageInfo(int amount, int skillId = 0, Element element = Element.Neutral, CastType castType = CastType.Projectile, bool isImpact = false, bool isCritical = false)
        {
            Amount = amount;
            SkillId = skillId;
            Element = element;
            CastType = castType;
            IsImpact = isImpact;
            IsCritical = isCritical;
        }

        /// <summary>피해량만 바꾼 복사본. 피해 파이프라인의 훅이 피해를 줄일 때 쓴다</summary>
        public DamageInfo WithAmount(int amount) => new DamageInfo(amount, SkillId, Element, CastType, IsImpact, IsCritical);

        /// <summary>치명타 피해로 바꾼 복사본. 피해량은 호출하는 쪽이 정한다</summary>
        public DamageInfo AsCritical(int amount) => new DamageInfo(amount, SkillId, Element, CastType, IsImpact, isCritical: true);

        /// <summary>지금 시전 중인 스킬(DamageAttribution 범위)의 속성과 시전 형태로 피해를 만든다. 스킬 밖이면 스킬 정보가 없는 피해가 된다.
        /// 스킬 피해는 치명타 판정(CriticalHit)을 거친다. 저항·취약·방어막보다 먼저이므로 치명타 피해도 그 계산을 받는다</summary>
        public static DamageInfo FromCurrent(int amount, bool isImpact = false)
        {
            var source = DamageAttribution.CurrentSource;
            var info = new DamageInfo(amount, source.SkillId, source.Element, source.CastType, isImpact);
            return source.IsSkill ? CriticalHit.Roll(info) : info;
        }
    }
}
