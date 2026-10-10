namespace Game.Core
{
    /// <summary>빙결이 유지되는 동안 매초 최대 HP 비례 피해를 받을 수 있는 대상 (매서운 서리)</summary>
    public interface IFreezeMaxHpDotTarget
    {
        void ApplyFreezeMaxHpDot(float maxHpRatio);
    }
}
