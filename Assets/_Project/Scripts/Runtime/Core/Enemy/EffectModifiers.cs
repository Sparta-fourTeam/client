using System;

namespace Game.Core
{
    /// <summary>몬스터가 받는 효과의 크기 배수. 몬스터 행의 Modifier 패시브에서 만든다. 1이면 변화가 없다</summary>
    public sealed class EffectModifiers
    {
        public static readonly EffectModifiers None = new EffectModifiers(1f, 1f);

        public float KnockbackMultiplier { get; }
        public float BurnDurationMultiplier { get; }

        public EffectModifiers(float knockbackMultiplier, float burnDurationMultiplier)
        {
            if (!IsValid(knockbackMultiplier))
            {
                throw new ArgumentOutOfRangeException(nameof(knockbackMultiplier), "배수는 0 이상의 유한한 값이어야 합니다.");
            }

            if (!IsValid(burnDurationMultiplier))
            {
                throw new ArgumentOutOfRangeException(nameof(burnDurationMultiplier), "배수는 0 이상의 유한한 값이어야 합니다.");
            }

            KnockbackMultiplier = knockbackMultiplier;
            BurnDurationMultiplier = burnDurationMultiplier;
        }

        private static bool IsValid(float value) => value >= 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
