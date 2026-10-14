using System;
using UnityEngine;

namespace Game.Core.Combat
{
    /// <summary>치명타 규칙. 스킬 피해가 만들어질 때(DamageInfo.FromCurrent) 확률로 피해를 배율만큼 늘리고 IsCritical을 켠다.
    /// 기본은 꺼져 있다(확률 0). 스테이지가 시작할 때 Combat 테이블 값으로 Configure를 부르고, 끝나면 Reset으로 되돌린다</summary>
    public static class CriticalHit
    {
        private static readonly Func<float> UnityRoll = () => UnityEngine.Random.value;

        /// <summary>치명타가 날 확률(0~1)</summary>
        public static float Chance { get; private set; }

        /// <summary>치명타 피해 배율(1 이상)</summary>
        public static float Multiplier { get; private set; } = 1f;

        /// <summary>0 이상 1 미만의 값을 돌려주는 난수. 테스트가 결과를 정하려고 바꾼다</summary>
        public static Func<float> Roller { get; set; } = UnityRoll;

        public static void Configure(float chance, float multiplier)
        {
            Chance = Mathf.Clamp01(chance);
            Multiplier = Mathf.Max(1f, multiplier);
        }

        /// <summary>꺼진 상태(치명타 없음)와 기본 난수로 되돌린다</summary>
        public static void Reset()
        {
            Chance = 0f;
            Multiplier = 1f;
            Roller = UnityRoll;
        }

        /// <summary>치명타가 나면 피해를 늘린 복사본을, 아니면 그대로 돌려준다. 피해가 0 이하이면 판정하지 않는다</summary>
        public static DamageInfo Roll(DamageInfo info)
        {
            if (Chance <= 0f || info.Amount <= 0 || info.IsCritical || Roller() >= Chance)
            {
                return info;
            }

            return info.AsCritical(Mathf.Max(info.Amount + 1, Mathf.RoundToInt(info.Amount * Multiplier)));
        }
    }
}
