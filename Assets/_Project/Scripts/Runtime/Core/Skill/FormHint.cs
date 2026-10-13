using System.Collections.Generic;

namespace Game.Core
{
    public enum FormHintKind
    {
        /// <summary>이 카드가 변환 카드의 조건이다 (필수 카드, 필수 스킬, 또는 그 스킬 자체를 얻는 카드)</summary>
        Enables,

        /// <summary>이 카드를 고르면 변환 카드를 더는 얻을 수 없다 (변환 카드의 배타 카드)</summary>
        Blocks,
    }

    /// <summary>카드 선택창에서 카드 아래에 보여 줄 형태 변환 하나. 변환 카드(효과에 form이 있는 강화 카드)와 이 카드의 관계를 담는다</summary>
    public readonly struct FormHint
    {
        /// <summary>변환 카드를 가진 스킬</summary>
        public SkillData Skill { get; }

        /// <summary>변환 카드</summary>
        public SkillUpgradeOption Card { get; }

        /// <summary>변환 카드를 고르면 바뀌는 형태</summary>
        public SkillForm Form { get; }

        public FormHintKind Kind { get; }

        public FormHint(SkillData skill, SkillUpgradeOption card, SkillForm form, FormHintKind kind)
        {
            Skill = skill;
            Card = card;
            Form = form;
            Kind = kind;
        }
    }

    /// <summary>카드 하나가 어떤 형태 변환의 조건이 되거나 막는지 찾는다. 조건 판정은 실제 획득 판정(<see cref="UpgradeEligibility"/>)과 같은 규칙을 쓴다.
    /// 이미 얻었거나 이번 판에서 더는 얻을 수 없는 변환(꺼짐, 영구 레벨 부족, 배타 카드 보유)은 보여 주지 않는다</summary>
    public static class FormHintFinder
    {
        private static readonly IReadOnlyList<FormHint> None = new FormHint[0];

        public static IReadOnlyList<FormHint> For(UpgradeChoice choice, IEnumerable<SkillData> catalog, IUpgradeState state)
        {
            if (choice.IsGeneral) { return None; }
            if (choice.IsNewWeapon)
            {
                return choice.newSkillData == null ? None : For(choice.newSkillData.id, null, catalog, state);
            }

            return choice.skill == null || choice.Option == null
                ? None
                : For(choice.skill.Data.id, choice.Option.id, catalog, state);
        }

        /// <summary>chosenCardId가 null이면 chosenSkillId 스킬을 새로 얻는 카드, 아니면 그 스킬의 강화 카드다</summary>
        public static IReadOnlyList<FormHint> For(int chosenSkillId, string chosenCardId, IEnumerable<SkillData> catalog, IUpgradeState state)
        {
            if (catalog == null || state == null) { return None; }

            var skills = new Dictionary<int, SkillData>();
            foreach (var data in catalog)
            {
                if (data != null) { skills[data.id] = data; }
            }

            List<FormHint> hints = null;
            foreach (var data in catalog)
            {
                if (data?.upgrades == null) { continue; }
                foreach (var card in data.upgrades)
                {
                    if (card == null || (data.id == chosenSkillId && card.id == chosenCardId)
                        || !TryGetForm(card, state.GetPermanentWeaponLevel(data.id), out var form)
                        || !Reachable(card, data.id, skills, state, 0, null, new HashSet<(int, string)>())) { continue; }

                    FormHintKind kind;
                    if (chosenCardId != null && !Reachable(card, data.id, skills, state, chosenSkillId, chosenCardId, new HashSet<(int, string)>())) { kind = FormHintKind.Blocks; }
                    else if (Enables(card, data.id, chosenSkillId, chosenCardId, state)) { kind = FormHintKind.Enables; }
                    else { continue; }

                    (hints ??= new List<FormHint>()).Add(new FormHint(data, card, form, kind));
                }
            }

            return hints ?? None;
        }

        /// <summary>지금 영구 레벨에서 고르면 바뀌는 형태. 형태를 바꾸지 않는 카드면 false</summary>
        public static bool TryGetForm(SkillUpgradeOption card, int permanentLevel, out SkillForm form)
        {
            form = SkillForm.Default;
            if (!SkillUpgradeResolver.TryResolve(card, permanentLevel, out var resolved)) { return false; }
            foreach (var effect in resolved.Effects)
            {
                if (effect.kind == "form" && (int)effect.value != (int)SkillForm.Default)
                {
                    form = (SkillForm)(int)effect.value;
                    return true;
                }
            }

            return false;
        }

        // 이 카드를 앞으로도 얻을 수 있는지. 전투 중에 바뀌지 않는 조건(꺼짐, 영구 레벨, 이미 가진 배타 카드)과 이미 얻은 횟수를 보고,
        // 아직 못 채운 선행 카드는 그 카드도 얻을 수 있어야 한다(선행 체인을 따라간다). 그래서 카드에 직접 적히지 않았어도
        // 선행 카드가 배타로 막히면 이 카드도 막힌 것으로 본다(뇌전 화살은 충격 화살이 선행이고 충격 화살은 폭발 화살과 배타).
        // 전투 레벨과 필수 스킬은 앞으로 채울 수 있으므로 보지 않는다.
        // chosenCardId가 있으면 그 카드를 지금 골랐다고 치고 판정한다(고르면 막히는 변환을 찾는다)
        private static bool Reachable(SkillUpgradeOption card, int skillId, IReadOnlyDictionary<int, SkillData> skills, IUpgradeState state,
            int chosenSkillId, string chosenCardId, HashSet<(int, string)> visiting)
        {
            if (!card.enabled || string.IsNullOrEmpty(card.id) || card.maxPickCount <= 0
                || state.GetAcquiredCount(skillId, card.id) >= card.maxPickCount
                || state.GetPermanentWeaponLevel(skillId) < card.minPermanentLevel
                || !visiting.Add((skillId, card.id))) { return false; }

            try
            {
                if (card.exclusions != null)
                {
                    foreach (var exclusion in card.exclusions)
                    {
                        if (exclusion == null || string.IsNullOrEmpty(exclusion.cardId)
                            || !UpgradeEligibility.ExclusionApplies(exclusion, skillId, state)) { continue; }
                        int owner = UpgradeEligibility.ExclusionOwner(exclusion, skillId);
                        if (state.GetAcquiredCount(owner, exclusion.cardId) > 0
                            || (owner == chosenSkillId && exclusion.cardId == chosenCardId)) { return false; }
                    }
                }

                if (card.requiredCardCounts != null)
                {
                    foreach (var requirement in card.requiredCardCounts)
                    {
                        if (requirement == null || string.IsNullOrEmpty(requirement.cardId)) { continue; }
                        int owner = requirement.skillId == 0 ? skillId : requirement.skillId;
                        if (state.GetAcquiredCount(owner, requirement.cardId) >= requirement.count) { continue; }
                        var required = skills.TryGetValue(owner, out var ownerData)
                            ? ownerData.upgrades?.Find(c => c != null && c.id == requirement.cardId)
                            : null;
                        if (required == null || required.maxPickCount < requirement.count
                            || !Reachable(required, owner, skills, state, chosenSkillId, chosenCardId, visiting)) { return false; }
                    }
                }

                return true;
            }
            finally { visiting.Remove((skillId, card.id)); }
        }

        // 새 스킬 카드는 그 스킬의 변환과 그 스킬을 필수로 하는 변환의 조건이다.
        // 강화 카드는 아직 채우지 못한 필수 카드(또는 필수 횟수)일 때만 조건이다
        private static bool Enables(SkillUpgradeOption card, int skillId, int chosenSkillId, string chosenCardId, IUpgradeState state)
        {
            if (chosenCardId == null)
            {
                if (skillId == chosenSkillId) { return true; }
                if (card.requiredWeaponIds != null)
                {
                    foreach (int requiredId in card.requiredWeaponIds)
                    {
                        if (requiredId == chosenSkillId) { return true; }
                    }
                }

                return false;
            }

            if (card.requiredCardCounts != null)
            {
                foreach (var requirement in card.requiredCardCounts)
                {
                    if (requirement == null || requirement.cardId != chosenCardId) { continue; }
                    int owner = requirement.skillId == 0 ? skillId : requirement.skillId;
                    if (owner == chosenSkillId && state.GetAcquiredCount(owner, chosenCardId) < requirement.count) { return true; }
                }
            }

            return false;
        }
    }
}
