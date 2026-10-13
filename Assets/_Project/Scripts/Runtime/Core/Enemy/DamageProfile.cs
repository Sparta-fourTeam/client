using System;
using System.Collections.Generic;
using Game.Core.Combat;

namespace Game.Core
{
    /// <summary>몬스터가 받는 피해를 속성과 시전 형태로 바꾸는 불변 표. Monsters 행의 Resists, CastResists, BlocksProjectile에서 만든다.
    /// 스킬에서 나온 피해(DamageInfo.SkillId가 0이 아님)에만 적용하고, 점화·동상 같은 상태이상 지속 피해는 바꾸지 않는다.
    /// 비율은 약점이 양수(받는 피해 +50% = 0.5), 저항이 음수(받는 피해 -70% = -0.7), -1이면 무효다</summary>
    public sealed class DamageProfile
    {
        public static readonly DamageProfile None = new DamageProfile(null, null, false);

        private static readonly int ElementCount = Enum.GetValues(typeof(Element)).Length;
        private static readonly int CastTypeCount = Enum.GetValues(typeof(CastType)).Length;

        private readonly float[] _elementMultipliers;
        private readonly float[] _castMultipliers;

        /// <summary>투사체의 직접 충격 피해를 무효로 하고 관통을 막는다. 부가 반응(폭발, 상태이상 등)은 막지 않는다</summary>
        public bool BlocksProjectile { get; }

        private DamageProfile(Dictionary<Element, float> elements, Dictionary<CastType, float> casts, bool blocksProjectile)
        {
            _elementMultipliers = ToMultipliers(elements, ElementCount);
            _castMultipliers = ToMultipliers(casts, CastTypeCount);
            BlocksProjectile = blocksProjectile;
        }

        private static float[] ToMultipliers<TKey>(Dictionary<TKey, float> ratios, int count) where TKey : struct, Enum
        {
            var multipliers = new float[count];
            for (int i = 0; i < count; i++) { multipliers[i] = 1f; }
            if (ratios != null)
            {
                foreach (var pair in ratios) { multipliers[Convert.ToInt32(pair.Key)] = Math.Max(0f, 1f + pair.Value); }
            }

            return multipliers;
        }

        /// <summary>몬스터 행에서 만든다. 키가 속성·시전 형태 이름이 아니거나 비율이 -1보다 작으면 InvalidOperationException</summary>
        public static DamageProfile From(MonsterDefinition monster)
        {
            bool empty = (monster.Resists == null || monster.Resists.Count == 0)
                && (monster.CastResists == null || monster.CastResists.Count == 0) && !monster.BlocksProjectile;
            if (empty)
            {
                return None;
            }

            var elements = Parse<Element>(monster.Id, "Resists", monster.Resists);
            var casts = Parse<CastType>(monster.Id, "CastResists", monster.CastResists);
            return new DamageProfile(elements, casts, monster.BlocksProjectile);
        }

        private static Dictionary<TKey, float> Parse<TKey>(int monsterId, string field, Dictionary<string, float> source) where TKey : struct, Enum
        {
            var result = new Dictionary<TKey, float>();
            if (source == null)
            {
                return result;
            }

            foreach (var pair in source)
            {
                if (!Enum.TryParse(pair.Key, false, out TKey key) || !Enum.IsDefined(typeof(TKey), key))
                {
                    throw new InvalidOperationException($"Monsters {monsterId}: {field}의 키 \"{pair.Key}\"은(는) {typeof(TKey).Name} 이름이 아닙니다");
                }

                if (float.IsNaN(pair.Value) || float.IsInfinity(pair.Value) || pair.Value < -1f)
                {
                    throw new InvalidOperationException($"Monsters {monsterId}: {field}[{pair.Key}]은(는) -1 이상의 숫자여야 합니다 (약점 +, 저항 -, -1은 무효)");
                }

                result[key] = pair.Value;
            }

            return result;
        }

        /// <summary>이 몬스터가 실제로 받는 피해량. 0이면 막힌 것이다</summary>
        public int Apply(DamageInfo info)
        {
            if (!info.IsFromSkill || ReferenceEquals(this, None))
            {
                return info.Amount;
            }

            if (BlocksProjectile && info.IsImpact && info.CastType == CastType.Projectile)
            {
                return 0;
            }

            double multiplier = (double)_elementMultipliers[(int)info.Element] * _castMultipliers[(int)info.CastType];
            if (multiplier == 1d)
            {
                return info.Amount;
            }

            if (multiplier <= 0d)
            {
                return 0;
            }

            // 줄어들어도 완전히 무효가 아니면 최소 1은 들어간다
            double scaled = Math.Floor(Math.Round(info.Amount * multiplier, 4));
            return (int)Math.Max(1d, Math.Min(int.MaxValue, scaled));
        }
    }
}
