using System.Collections.Generic;
using System.Globalization;

namespace Game.Core
{
    /// <summary>강화 효과(Kind, ValuePerLevel)를 팝업 한 줄 글자로 바꾼다. 효과 이름은 아직 고정 텍스트다(로컬라이징이 들어오면 키 조회로 바꾼다)</summary>
    public static class EquipEffectText
    {
        private static readonly Dictionary<string, string> Labels = new()
        {
            ["skillDamagePercent"] = "[기술력] 스킬 대미지",
        };

        /// <summary>현재 레벨의 효과량과, 최대 레벨이 아니면 다음 레벨에서 늘어나는 양을 보여준다. 예: "[기술력] 스킬 대미지 15% ▲ (+5%)"</summary>
        public static string Format(UpgradeStatEffect effect, int level, bool isMaxLevel)
        {
            string label = Labels.TryGetValue(effect.Kind, out var known) ? known : effect.Kind;
            string unit = effect.Kind.EndsWith("Percent") ? "%" : string.Empty;
            string text = $"{label} {Number(effect.ValuePerLevel * level)}{unit} ▲";
            return isMaxLevel ? text : $"{text} (+{Number(effect.ValuePerLevel)}{unit})";
        }

        private static string Number(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
