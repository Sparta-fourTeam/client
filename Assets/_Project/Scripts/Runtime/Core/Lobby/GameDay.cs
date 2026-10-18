using System;
using System.Globalization;

namespace Game.Core
{
    /// <summary>UTC 시각에서 한국시간 04시 기준 일일·주간 날짜와 다음 초기화를 계산한다.</summary>
    public static class GameDay
    {
        public const int ResetHour = 4;
        private const int KoreaUtcOffsetHours = 9;

        /// <summary>UTC 입력의 한국시간 영업일. 시간대 없는 입력도 UTC로 취급한다.</summary>
        public static DateTime Date(DateTime utcNow) =>
            AsUtc(utcNow).AddHours(KoreaUtcOffsetHours - ResetHour).Date;

        public static string Key(DateTime utcNow) => Date(utcNow).ToString("yyyyMMdd", CultureInfo.InvariantCulture);

        /// <summary>다음 한국시간 04시의 UTC 시각.</summary>
        public static DateTime NextResetUtc(DateTime utcNow) =>
            Date(utcNow).AddDays(1).AddHours(ResetHour - KoreaUtcOffsetHours);

        private static DateTime AsUtc(DateTime value) => value.Kind == DateTimeKind.Local
            ? value.ToUniversalTime()
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }
}
