using System;
using System.Collections.Generic;

namespace Game.Core
{
    public struct UpgradeChoice
    {
        public bool IsNewWeapon;
        public WeaponData NewWeaponData;
        public WeaponBase Weapon;
        public WeaponUpgradeOption Option;
        public string DisplayName;
        public string DisplayDescription;
    }

    /// <summary>Builds and validates battle choices independently of the Unity controller.</summary>
    public sealed class WeaponUpgradeChoices
    {
        private readonly IReadOnlyList<WeaponBase> weapons;
        private readonly IEnumerable<WeaponData> catalog;
        private readonly IUpgradeState state;
        private readonly Func<int, bool> hasPrefab;

        public WeaponUpgradeChoices(IReadOnlyList<WeaponBase> weapons, IEnumerable<WeaponData> catalog,
            IUpgradeState state, Func<int, bool> hasPrefab)
        {
            this.weapons = weapons;
            this.catalog = catalog;
            this.state = state;
            this.hasPrefab = hasPrefab;
        }

        public List<UpgradeChoice> Select(int count, Func<int, int> nextIndex)
        {
            var candidates = new List<UpgradeChoice>();
            if (count <= 0) { return candidates; }
            var sharedGroups = new HashSet<string>();
            foreach (var weapon in weapons)
            {
                if (weapon.IsMaxLevel || weapon.Data.upgrades == null) { continue; }
                foreach (var option in weapon.Data.upgrades)
                {
                    if (!CanApply(weapon, option)
                        || !WeaponUpgradeResolver.TryResolve(option, state.GetPermanentWeaponLevel(weapon.Data.id), out var resolved)) { continue; }
                    if (!string.IsNullOrEmpty(option.sharedId) && !sharedGroups.Add(option.sharedId)) { continue; }
                    candidates.Add(new UpgradeChoice
                    {
                        Weapon = weapon,
                        Option = option,
                        DisplayName = resolved.Name,
                        DisplayDescription = resolved.Description
                    });
                }
            }
            foreach (var data in catalog)
            {
                if (state.GetWeaponLevel(data.id) == 0 && hasPrefab(data.id))
                {
                    candidates.Add(new UpgradeChoice { IsNewWeapon = true, NewWeaponData = data });
                }
            }
            for (int i = candidates.Count - 1; i >= 0; i--)
            {
                int j = nextIndex(i + 1);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            }
            return candidates.GetRange(0, Math.Min(count, candidates.Count));
        }

        public bool TryApply(UpgradeChoice choice) => !choice.IsNewWeapon && CanApply(choice.Weapon, choice.Option)
            && WeaponUpgradeTransaction.TryApply(choice.Weapon, choice.Option, weapons,
                state.GetPermanentWeaponLevel(choice.Weapon.Data.id));

        private bool CanApply(WeaponBase weapon, WeaponUpgradeOption option)
        {
            if (weapon == null || option == null || weapon.Data.upgrades == null || !weapon.Data.upgrades.Contains(option)) { return false; }
            bool owned = false;
            foreach (var candidate in weapons)
            {
                if (ReferenceEquals(candidate, weapon)) { owned = true; break; }
            }
            return owned && UpgradeEligibility.CanAcquire(option, weapon.Data.id, state)
                && WeaponUpgradeTransaction.CanApply(weapon, option, weapons, state.GetPermanentWeaponLevel(weapon.Data.id));
        }
    }
}
