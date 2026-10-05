using System;
using System.Globalization;
using System.Reflection;
using System.Text;
using Game.Core;

namespace Game.Sandbox
{
    /// <summary>스킬의 현재 스탯을 사람이 읽는 글로 펼친다. 리플렉션으로 읽으므로 새 스탯 묶음이 생겨도 따로 고치지 않는다.</summary>
    public static class SandboxStatsText
    {
        public static string Describe(SkillBase skill)
        {
            if (skill == null) { return "(스킬 없음)"; }
            var text = new StringBuilder();
            var data = skill.Data;
            text.Append(data.name).Append("  [").Append(data.castType).Append("]  Lv ").Append(skill.Level).Append('\n');
            foreach (var group in typeof(SkillStats).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                object value = group.GetValue(skill.Stats);
                var parts = new StringBuilder();
                foreach (var field in group.PropertyType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    string formatted = Format(field.GetValue(value));
                    if (formatted == "0" || formatted == "false") { continue; }
                    if (parts.Length > 0) { parts.Append("  "); }
                    parts.Append(field.Name).Append(' ').Append(formatted);
                }
                if (parts.Length > 0) { text.Append(group.Name).Append(": ").Append(parts).Append('\n'); }
            }
            var config = skill.Config;
            if (config.Children.Count > 0)
            {
                text.Append("자식: ");
                bool first = true;
                foreach (var pair in config.Children)
                {
                    if (!first) { text.Append(", "); }
                    first = false;
                    text.Append(pair.Key);
                }
                text.Append('\n');
            }
            return text.ToString().TrimEnd('\n');
        }

        private static string Format(object value) => value switch
        {
            float f => Math.Round(f, 3).ToString(CultureInfo.InvariantCulture),
            bool b => b ? "true" : "false",
            _ => Convert.ToString(value, CultureInfo.InvariantCulture)
        };
    }
}
