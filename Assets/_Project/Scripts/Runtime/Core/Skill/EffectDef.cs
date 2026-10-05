namespace Game.Core
{
    /// <summary>카드가 가진 효과 하나. kind는 <see cref="EffectRegistry"/>에 등록된 키(예: "damage")다.</summary>
    public class EffectDef
    {
        public string kind;
        public float value;
    }
}
