using System.Collections.Generic;
using System.Globalization;

namespace Game.Core
{
    /// <summary>강화 효과(Kind, ValuePerLevel)를 팝업 글자로 바꾼다. 효과 이름은 종류별 로컬라이즈 키로 조회한다</summary>
    public static class EquipEffectText
    {
        private static readonly Dictionary<string, string> LabelKeys = new()
        {
            ["skillDamagePercent"] = "equip.effectSkillDamageFull",
        };

        // 능력치 줄에 쓰는 짧은 이름
        private static readonly Dictionary<string, string> NameKeys = new()
        {
            ["skillDamagePercent"] = "equip.effectSkillDamage",
        };

        /// <summary>현재 레벨의 효과량과, 최대 레벨이 아니면 다음 레벨에서 늘어나는 양을 보여준다. 예: "[기술력] 스킬 대미지 15% ▲ (+5%)"</summary>
        public static string Format(UpgradeStatEffect effect, int level, bool isMaxLevel)
        {
            string label = LabelKeys.TryGetValue(effect.Kind, out var labelKey) ? L10n.Get(labelKey) : effect.Kind;
            string text = $"{label} {Value(effect, level)} ▲";
            return isMaxLevel ? text : $"{text} (+{Format(effect, effect.ValueAt(level + 1) - effect.ValueAt(level))})";
        }

        /// <summary>능력치 줄의 이름. 예: "스킬 대미지". 모르는 종류는 Kind를 그대로 쓴다</summary>
        public static string Name(UpgradeStatEffect effect) => NameKeys.TryGetValue(effect.Kind, out var nameKey) ? L10n.Get(nameKey) : effect.Kind;

        /// <summary>level의 효과량. 예: 레벨 3, 레벨당 5% → "15%"</summary>
        public static string Value(UpgradeStatEffect effect, int level) => Format(effect, effect.ValueAt(level));

        private static string Format(UpgradeStatEffect effect, float value) =>
            $"{Number(value)}{(effect.Kind.EndsWith("Percent") ? "%" : string.Empty)}";

        private static string Number(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
