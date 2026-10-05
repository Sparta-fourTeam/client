using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>Validates catalog identities, shared upgrades, and the prerequisite graph.</summary>
    public static class WeaponCatalogValidator
    {
        public static void Validate(List<WeaponData> weapons)
        {
            if (weapons == null || weapons.Count == 0)
            {
                throw new InvalidOperationException("무기 카탈로그가 비어 있습니다.");
            }

            var ids = new HashSet<int>();
            var cards = new HashSet<string>();
            foreach (var weapon in weapons)
            {
                if (weapon == null || !ids.Add(weapon.id) || weapon.baseStats == null
                    || weapon.maxLevel < 1 || weapon.upgrades == null)
                {
                    throw new InvalidOperationException("무기 정의 또는 ID가 잘못되었습니다.");
                }

                foreach (var option in weapon.upgrades)
                {
                    if (option == null || string.IsNullOrEmpty(option.id) || !cards.Add(option.id)
                        || option.maxPickCount <= 0 || !WeaponUpgradeResolver.TryResolve(option, 0, out _))
                    {
                        throw new InvalidOperationException("강화 정의 또는 ID가 잘못되었습니다.");
                    }
                    ValidateEffectCompatibility(weapon, option);
                }
            }
            foreach (var weapon in weapons)
            {
                foreach (var option in weapon.upgrades)
                {
                    if (!string.IsNullOrEmpty(option.sharedId) && cards.Contains(option.sharedId))
                    {
                        throw new InvalidOperationException("공유 강화 키가 카드 ID와 충돌합니다.");
                    }
                }
            }
            WeaponUpgradeTransaction.ValidateCatalog(weapons);
            ValidateConditions(weapons);
        }

        // 공격이 소비하지 않는 효과는 적용돼도 아무 일이 없으므로, 어느 카드가 문제인지 알려 주며 막는다.
        private static void ValidateEffectCompatibility(WeaponData weapon, WeaponUpgradeOption option)
        {
            Check(weapon, option, option.effects);
            if (option.variants == null) { return; }
            foreach (var variant in option.variants) { Check(weapon, option, variant.effects); }
        }

        private static void Check(WeaponData weapon, WeaponUpgradeOption option, List<StatEffect> effects)
        {
            foreach (var effect in effects)
            {
                if (UpgradeCompatibility.Supports(weapon.castType, effect.type)) { continue; }
                string reason = UpgradeCompatibility.IsRegistered(effect.type)
                    ? $"{weapon.castType} 공격이 쓰지 않는 효과입니다."
                    : "호환 표(UpgradeCompatibility)에 등록되지 않았습니다.";
                throw new InvalidOperationException($"카드 '{option.id}'({weapon.name})의 효과 {effect.type}: {reason}");
            }
        }

        private static void ValidateConditions(List<WeaponData> weapons)
        {
            var byId = new Dictionary<int, WeaponData>();
            var owners = new Dictionary<string, int>();
            var options = new Dictionary<string, WeaponUpgradeOption>();
            var edges = new Dictionary<string, List<string>>();
            foreach (var weapon in weapons)
            {
                byId.Add(weapon.id, weapon);
                foreach (var option in weapon.upgrades)
                {
                    owners.Add(option.id, weapon.id);
                    options.Add(option.id, option);
                    edges.Add(option.id, new List<string>());
                }
            }
            foreach (var weapon in weapons)
            {
                foreach (var option in weapon.upgrades)
                {
                    // 미연결 카드는 사유를 남겨 비활성으로 보존할 수 있다.
                    if (!option.enabled) { continue; }
                    if (option.minPermanentLevel < 0 || option.minBattleLevel < 1
                        || option.minBattleLevel > weapon.maxLevel + 1) { Invalid(); }
                    if (option.requiredWeaponIds != null)
                    {
                        foreach (int id in option.requiredWeaponIds)
                        {
                            if (!byId.ContainsKey(id)) { Invalid(); }
                        }
                    }
                    if (option.requiredCardIds != null)
                    {
                        foreach (string id in option.requiredCardIds) { Require(id, weapon.id, 1); }
                    }
                    if (option.requiredCardCounts != null)
                    {
                        foreach (var r in option.requiredCardCounts)
                        {
                            if (r == null) { Invalid(); }
                            Require(r.cardId, r.weaponId == 0 ? weapon.id : r.weaponId, r.count);
                        }
                    }
                    if (option.exclusions != null)
                    {
                        foreach (var e in option.exclusions)
                        {
                            if (e == null || e.belowPermanentLevel < 0 || string.IsNullOrEmpty(e.cardId)
                                || !owners.TryGetValue(e.cardId, out var owner)
                                || owner != (e.weaponId == 0 ? weapon.id : e.weaponId)) { Invalid(); }
                        }
                    }
                    void Require(string id, int owner, int count)
                    {
                        if (string.IsNullOrEmpty(id) || id == option.id || count <= 0
                            || !owners.TryGetValue(id, out var actualOwner) || actualOwner != owner
                            || !options[id].enabled || count > options[id].maxPickCount) { Invalid(); }
                        edges[option.id].Add(id);
                    }
                }
            }
            var visiting = new HashSet<string>();
            var visited = new HashSet<string>();
            foreach (var id in options.Keys) { Visit(id); }
            void Visit(string id)
            {
                if (visited.Contains(id)) { return; }
                if (!visiting.Add(id)) { Invalid(); }
                foreach (string dependency in edges[id]) { Visit(dependency); }
                visiting.Remove(id);
                visited.Add(id);
            }
            void Invalid() => throw new InvalidOperationException("강화 조건의 참조, 횟수, 레벨 또는 순환이 잘못되었습니다.");
        }
    }
}
