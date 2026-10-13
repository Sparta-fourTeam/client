using System;

namespace Game.Core
{
    /// <summary>몬스터가 걸리지 않는 상태이상. 효과를 구현할 때마다 항목을 늘린다</summary>
    [Flags]
    public enum StatusImmunity
    {
        None = 0,
        Stun = 1 << 0,
        Burn = 1 << 1,
        Paralysis = 1 << 2,
        Freeze = 1 << 3,
        Slow = 1 << 4,
        Frostbite = 1 << 5,
        Vulnerability = 1 << 6,

        /// <summary>모든 디버프(위의 상태이상 전부)</summary>
        AllDebuffs = Stun | Burn | Paralysis | Freeze | Slow | Frostbite | Vulnerability,
    }
}
