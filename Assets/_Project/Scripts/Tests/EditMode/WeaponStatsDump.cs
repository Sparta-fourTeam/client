using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Game.Core;

namespace Game.Tests
{
    /// <summary>WeaponStats의 모든 값을 "그룹.이름" 키로 펼친다. 리플렉션으로 읽으므로 새 스탯이 생기면 자동으로 포함된다.</summary>
    internal static class WeaponStatsDump
    {
        public static SortedDictionary<string, string> Flatten(WeaponStats stats)
        {
            var map = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var group in typeof(WeaponStats).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                object value = group.GetValue(stats);
                foreach (var field in group.PropertyType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    map[$"{group.Name}.{field.Name}"] = Format(field.GetValue(value));
                }
            }
            return map;
        }

        /// <summary>before와 값이 달라진 항목만 "키=값; 키=값" 형태로 돌려준다. 변화가 없으면 "(변화 없음)".</summary>
        public static string Diff(IReadOnlyDictionary<string, string> before, IReadOnlyDictionary<string, string> after)
        {
            var changed = after.Where(p => !before.TryGetValue(p.Key, out var old) || old != p.Value)
                .Select(p => $"{p.Key}={p.Value}").ToList();
            return changed.Count == 0 ? "(변화 없음)" : string.Join("; ", changed);
        }

        private static string Format(object value) => value switch
        {
            float f => f.ToString("R", CultureInfo.InvariantCulture),
            double d => d.ToString("R", CultureInfo.InvariantCulture),
            bool b => b ? "true" : "false",
            _ => Convert.ToString(value, CultureInfo.InvariantCulture)
        };
    }
}
