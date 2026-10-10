namespace Game.Core
{
    /// <summary>한 번에 가질 수 있는 스킬 수. 게임 로직(획득 제한, 새 스킬 카드 후보)과 샌드박스가 이 값을 같이 쓰고,
    /// 스킬 HUD 프리팹의 슬롯 수는 이 값과 같아야 한다 (테스트가 확인한다). 자식 스킬은 슬롯을 차지하지 않는다.</summary>
    public static class SkillSlotLimit
    {
        public const int Max = 5;
    }
}
