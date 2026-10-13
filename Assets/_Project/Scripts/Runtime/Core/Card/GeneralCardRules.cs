using System;
using System.Collections.Generic;
using Game.Core.Defense;

namespace Game.Core
{
    /// <summary>일반 카드의 조건과 효과가 쓰는 현재 스테이지 상태. 새 종류가 다른 상태를 필요로 하면 여기에 추가한다</summary>
    public sealed class GeneralCardContext
    {
        public Wall Wall { get; }

        public GeneralCardContext(Wall wall)
        {
            Wall = wall;
        }
    }

    /// <summary>일반 카드의 조건(언제 나오는가)과 효과(고르면 무엇이 되는가)를 이름으로 등록해 둔 곳.
    /// 새 종류는 여기에 한 줄 추가하고 GeneralCards.json에서 그 이름을 쓰면 된다</summary>
    public static class GeneralCardRules
    {
        private static readonly Dictionary<string, Func<GeneralCardContext, float, bool>> Conditions = new()
        {
            // 방벽 체력이 value% 이하
            ["wallHpBelow"] = (context, value) =>
                context.Wall != null && !context.Wall.IsDestroyed && context.Wall.MaxHp > 0
                && context.Wall.CurrentHp * 100f <= value * context.Wall.MaxHp,
        };

        private static readonly Dictionary<string, Action<GeneralCardContext, float>> Effects = new()
        {
            // 방벽 체력을 value만큼 회복
            ["wallRepair"] = (context, value) => context.Wall.Repair((int)value),
        };

        public static bool HasCondition(string kind) => kind != null && Conditions.ContainsKey(kind);

        public static bool HasEffect(string kind) => kind != null && Effects.ContainsKey(kind);

        public static bool IsMet(GeneralCardRule condition, GeneralCardContext context) =>
            condition == null || Conditions[condition.Kind](context, condition.Value);

        public static void Apply(GeneralCardRule effect, GeneralCardContext context) =>
            Effects[effect.Kind](context, effect.Value);
    }
}
