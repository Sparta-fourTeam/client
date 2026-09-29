using System;
using System.Globalization;

namespace Game.Core
{
    /// <summary>저장된 시각 이후 경과 시간으로 현재 에너지와 다음 회복까지 남은 시간을 계산한다</summary>
    public static class EnergyRule
    {
        /// <summary>energyUpdatedAt 문자열을 UTC로 해석한다. 비어 있으면(Apply 전) DateTime.UtcNow를 반환한다</summary>
        public static DateTime ParseUpdatedAt(string value) =>
            string.IsNullOrEmpty(value)
                ? DateTime.UtcNow
                : DateTime.Parse(value, CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);

        public static (int current, float nextIn) At(int stored, DateTime updatedAt, DateTime now, EnergyConfig c)
        {
            if (stored >= c.Max)
            {
                return (stored, 0);
            }

            double elapsed = Math.Max(0, (now - updatedAt).TotalSeconds);
            int gained = (int)(elapsed / c.RegenSeconds);
            int current = Math.Min(c.Max, stored + gained);
            float nextIn = current >= c.Max ? 0 : (float)(c.RegenSeconds - elapsed % c.RegenSeconds);
            return (current, nextIn);
        }
    }
}
