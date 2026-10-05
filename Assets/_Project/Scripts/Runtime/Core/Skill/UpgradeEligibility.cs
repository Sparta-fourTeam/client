namespace Game.Core
{
    /// <summary>후보 생성과 실제 습득에서 공유하는, 상태를 변경하지 않는 조건 판정.</summary>
    public static class UpgradeEligibility
    {
        public static bool CanAcquire(SkillUpgradeOption option, int weaponId, IUpgradeState state)
        {
            if (option == null || state == null || !option.enabled || string.IsNullOrEmpty(option.id)
                || option.maxPickCount <= 0 || option.minBattleLevel < 1 || option.minPermanentLevel < 0
                || state.GetWeaponLevel(weaponId) < option.minBattleLevel
                || state.GetPermanentWeaponLevel(weaponId) < option.minPermanentLevel
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

            if (option.requiredCardIds != null)
            {
                foreach (var cardId in option.requiredCardIds)
                {
                    if (string.IsNullOrEmpty(cardId) || cardId == option.id
                        || state.GetAcquiredCount(weaponId, cardId) < 1) { return false; }
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
                    int owner = exclusion.skillId == 0 ? weaponId : exclusion.skillId;
                    bool applies = exclusion.belowPermanentLevel == 0
                        || state.GetPermanentWeaponLevel(weaponId) < exclusion.belowPermanentLevel;
                    if (applies && state.GetAcquiredCount(owner, exclusion.cardId) > 0) { return false; }
                }
            }

            return true;
        }
    }
}
