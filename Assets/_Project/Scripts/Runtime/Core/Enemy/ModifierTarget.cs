namespace Game.Core
{
    /// <summary>Modifier 패시브가 배수를 곱하는 대상. 몬스터가 받는 효과의 크기를 바꾼다</summary>
    public enum ModifierTarget
    {
        /// <summary>밀치기 거리. 0이면 밀려나지 않는다</summary>
        KnockbackDistance,

        /// <summary>점화 지속시간. 4면 4배로 오래 탄다</summary>
        BurnDuration,
    }
}
