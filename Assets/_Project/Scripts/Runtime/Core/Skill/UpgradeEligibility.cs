namespace Game.Core
{
    /// <summary>후보 생성과 실제 습득에서 공유하는, 상태를 변경하지 않는 조건 판정.</summary>
    public static class UpgradeEligibility
    {
        /// <param name="ignorePermanentLevel">true면 카드의 영구 레벨 조건(minPermanentLevel)을 보지 않는다. 영구 레벨을 따로 정할 수 있는 도구(샌드박스)가 조건을 표시만 하고 막지 않을 때 쓴다</param>
        public static bool CanAcquire(SkillUpgradeOption option, int weaponId, IUpgradeState state, bool ignorePermanentLevel = false)
        {
            if (option == null || state == null || !option.enabled || string.IsNullOrEmpty(option.id)
                || option.maxPickCount <= 0 || option.minBattleLevel < 1 || option.minPermanentLevel < 0
                || state.GetWeaponLevel(weaponId) < option.minBattleLevel
                || (!ignorePermanentLevel && state.GetPermanentWeaponLevel(weaponId) < option.minPermanentLevel)
                || state.GetAcquiredCount(weaponId, option.id) >= option.maxPickCount)
            {
                return false;
            }

            if (option.requiredWeaponIds != null)
            {
                foreach (var requiredId in option.requiredWeaponIds)
                {
                    if (state.GetWeaponLevel(requiredId) <= 0) { return false; }
                }
            }

            if (option.requiredCardCounts != null)
            {
                foreach (var requirement in option.requiredCardCounts)
                {
                    if (requirement == null || string.IsNullOrEmpty(requirement.cardId)
                        || requirement.count <= 0) { return false; }
                    int owner = requirement.skillId == 0 ? weaponId : requirement.skillId;
                    if ((owner == weaponId && requirement.cardId == option.id)
                        || state.GetAcquiredCount(owner, requirement.cardId) < requirement.count) { return false; }
                }
            }

            if (option.exclusions != null)
            {
                foreach (var exclusion in option.exclusions)
                {
                    if (exclusion == null || string.IsNullOrEmpty(exclusion.cardId)
                        || exclusion.belowPermanentLevel < 0) { return false; }
                    if (ExclusionApplies(exclusion, weaponId, state)
                        && state.GetAcquiredCount(ExclusionOwner(exclusion, weaponId), exclusion.cardId) > 0) { return false; }
                }
            }

            return true;
        }

        /// <summary>배타 카드의 주인 스킬 ID. skillId가 0이면 배타를 가진 카드의 스킬이다</summary>
        public static int ExclusionOwner(CardExclusion exclusion, int weaponId) =>
            exclusion.skillId == 0 ? weaponId : exclusion.skillId;

        /// <summary>지금 영구 레벨에서 이 배타가 살아 있는지. 배타 카드를 이미 가졌는지는 보지 않는다</summary>
        public static bool ExclusionApplies(CardExclusion exclusion, int weaponId, IUpgradeState state) =>
            exclusion.belowPermanentLevel == 0
            || state.GetPermanentWeaponLevel(weaponId) < exclusion.belowPermanentLevel;
    }
}
