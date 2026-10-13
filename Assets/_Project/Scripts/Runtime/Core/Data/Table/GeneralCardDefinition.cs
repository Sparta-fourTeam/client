using System;

namespace Game.Core
{
    /// <summary>일반 카드의 조건이나 효과 한 줄. Kind는 GeneralCardRules에 등록된 이름이다</summary>
    [Serializable]
    public class GeneralCardRule
    {
        public string Kind;
        public float Value;
    }

    /// <summary>스킬이 아닌 카드(방벽 회복 등) 테이블 한 행. 스킬 카드는 Skills.json의 upgrades에 있다.
    /// 카드 3장을 뽑을 때 스킬 후보와 함께 후보가 되고, Forced이면 조건이 맞을 때 자리를 하나 차지한다</summary>
    [Serializable]
    public class GeneralCardDefinition
    {
        public string Id;
        public string Name;
        public string Desc;
        public string IconKey;

        /// <summary>한 판에서 고를 수 있는 최대 횟수</summary>
        public int MaxPicks;

        /// <summary>true이면 조건이 맞을 때 3장 중 한 칸을 먼저 차지한다. false이면 스킬 후보와 같은 후보 목록에서 섞인다</summary>
        public bool Forced;

        /// <summary>Forced 카드가 여럿 나올 수 있을 때 큰 값이 먼저 뽑힌다. 같으면 무작위</summary>
        public int Priority;

        /// <summary>나올 수 있는 조건. 없으면 항상 나올 수 있다</summary>
        public GeneralCardRule Condition;

        public GeneralCardRule Effect;

        public void Validate()
        {
            if (string.IsNullOrEmpty(Id) || string.IsNullOrEmpty(Name))
            {
                throw new InvalidOperationException($"GeneralCards '{Id}': Id와 Name이 필요합니다");
            }

            if (MaxPicks < 1)
            {
                throw new InvalidOperationException($"GeneralCards {Id}: MaxPicks는 1 이상이어야 합니다");
            }

            if (Effect == null || !GeneralCardRules.HasEffect(Effect.Kind))
            {
                throw new InvalidOperationException($"GeneralCards {Id}: 등록되지 않은 효과 종류 '{Effect?.Kind}'");
            }

            if (Condition != null && !GeneralCardRules.HasCondition(Condition.Kind))
            {
                throw new InvalidOperationException($"GeneralCards {Id}: 등록되지 않은 조건 종류 '{Condition.Kind}'");
            }
        }
    }
}
