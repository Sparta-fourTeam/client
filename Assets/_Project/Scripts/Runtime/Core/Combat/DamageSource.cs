namespace Game.Core.Combat
{
    /// <summary>피해가 어느 스킬에서 나왔는지와 그 스킬의 속성·시전 형태.
    /// DamageAttribution의 범위가 들고 다니므로, 자식 스킬의 피해는 가장 바깥 스킬(부모)의 값을 이어받는다.
    /// SkillId가 0이면 스킬 밖에서 나온 피해다</summary>
    public readonly struct DamageSource
    {
        public int SkillId { get; }
        public Element Element { get; }
        public CastType CastType { get; }

        public DamageSource(int skillId, Element element = Element.Neutral, CastType castType = CastType.Projectile)
        {
            SkillId = skillId;
            Element = element;
            CastType = castType;
        }

        public bool IsSkill => SkillId != 0;
    }
}
