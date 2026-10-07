using System;
using System.Collections.Generic;

namespace Game.Core
{
    public struct UpgradeChoice
    {
        public bool IsNewWeapon;
        public SkillData newSkillData;
        public SkillBase skill;
        public SkillUpgradeOption Option;
        public string DisplayName;
        public string DisplayDescription;

        /// <summary>스킬이 아닌 일반 카드(방벽 회복 등)이면 그 정의. 이때 Weapon, Option, NewWeaponData는 비어 있다</summary>
        public GeneralCardDefinition GeneralCard;

        public bool IsGeneral => GeneralCard != null;
    }

    /// <summary>Builds and validates battle choices independently of the Unity controller.</summary>
    public sealed class SkillUpgradeChoices
    {
        private readonly IReadOnlyList<SkillBase> skills;
        private readonly IEnumerable<SkillData> catalog;
        private readonly IUpgradeState state;
        private readonly Func<int, bool> hasPrefab;
        private readonly Func<SkillData, bool> isUnlocked;

        /// <param name="isUnlocked">새 스킬 카드로 내놓을 수 있는(열린) 스킬인지. null이면 모든 스킬이 열린 것으로 본다.
        /// 이미 가진 스킬의 강화 카드는 이 조건과 상관없다</param>
        public SkillUpgradeChoices(IReadOnlyList<SkillBase> skills, IEnumerable<SkillData> catalog,
            IUpgradeState state, Func<int, bool> hasPrefab, Func<SkillData, bool> isUnlocked = null)
        {
            this.skills = skills;
            this.catalog = catalog;
            this.state = state;
            this.hasPrefab = hasPrefab;
            this.isUnlocked = isUnlocked;
        }

        public List<UpgradeChoice> Select(int count, Func<int, int> nextIndex)
        {
            if (count <= 0) { return new List<UpgradeChoice>(); }
            var candidates = Candidates();
            for (int i = candidates.Count - 1; i >= 0; i--)
            {
                int j = nextIndex(i + 1);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            }
            return candidates.GetRange(0, Math.Min(count, candidates.Count));
        }

        /// <summary>지금 고를 수 있는 스킬 카드 전부 (섞지 않은 채)</summary>
        public List<UpgradeChoice> Candidates()
        {
            var candidates = new List<UpgradeChoice>();
            var sharedGroups = new HashSet<string>();
            foreach (var weapon in skills)
            {
                if (weapon.IsMaxLevel || weapon.Data.upgrades == null) { continue; }
                foreach (var option in weapon.Data.upgrades)
                {
                    if (!CanApply(weapon, option)
                        || !SkillUpgradeResolver.TryResolve(option, state.GetPermanentWeaponLevel(weapon.Data.id), out var resolved)) { continue; }
                    if (!string.IsNullOrEmpty(option.sharedId) && !sharedGroups.Add(option.sharedId)) { continue; }
                    candidates.Add(new UpgradeChoice
                    {
                        skill = weapon,
                        Option = option,
                        DisplayName = resolved.Name,
                        DisplayDescription = resolved.Description
                    });
                }
            }
            foreach (var data in catalog)
            {
                if (!data.childOnly && state.GetWeaponLevel(data.id) == 0 && hasPrefab(data.id)
                    && (isUnlocked == null || isUnlocked(data)))
                {
                    candidates.Add(new UpgradeChoice { IsNewWeapon = true, newSkillData = data });
                }
            }
            return candidates;
        }

        public bool TryApply(UpgradeChoice choice) => !choice.IsNewWeapon && CanApply(choice.skill, choice.Option)
            && SkillUpgradeTransaction.TryApply(choice.skill, choice.Option, skills,
                state.GetPermanentWeaponLevel(choice.skill.Data.id));

        private bool CanApply(SkillBase skill, SkillUpgradeOption option)
        {
            if (skill == null || option == null || skill.Data.upgrades == null || !skill.Data.upgrades.Contains(option)) { return false; }
            bool owned = false;
            foreach (var candidate in skills)
            {
                if (ReferenceEquals(candidate, skill)) { owned = true; break; }
            }
            return owned && UpgradeEligibility.CanAcquire(option, skill.Data.id, state)
                && SkillUpgradeTransaction.CanApply(skill, option, skills, state.GetPermanentWeaponLevel(skill.Data.id));
        }
    }
}
