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
                if (!WeaponFactory.IsRegistered(weapon.castType))
                {
                    throw new InvalidOperationException($"무기 '{weapon.name}'의 공격 종류 {weapon.castType}에 등록된 공격 전략이 없습니다.");
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
            ValidateChildSkills(weapons);
            ValidateChildOverlays(weapons);
        }

        // 자식 스킬 시전 효과는 존재하는 스킬만 가리켜야 하고, 자식이 부모를 다시 시전하는 순환이 없어야 한다.
        private static void ValidateChildSkills(List<WeaponData> weapons)
        {
            var ids = new HashSet<int>();
            foreach (var weapon in weapons) { ids.Add(weapon.id); }
            var children = new Dictionary<int, List<int>>();
            foreach (var weapon in weapons)
            {
                var list = new List<int>();
                children[weapon.id] = list;
                foreach (var option in weapon.upgrades)
                {
                    CollectChildren(weapon, option, option.effects, ids, list);
                    if (option.variants == null) { continue; }
                    foreach (var variant in option.variants) { CollectChildren(weapon, option, variant.effects, ids, list); }
                }
            }
            var visiting = new List<int>();
            var done = new HashSet<int>();
            foreach (var id in children.Keys) { Visit(id); }
            void Visit(int id)
            {
                if (done.Contains(id)) { return; }
                if (visiting.Contains(id))
                {
                    int start = visiting.IndexOf(id);
                    var path = visiting.GetRange(start, visiting.Count - start);
                    path.Add(id);
                    throw new InvalidOperationException($"자식 스킬 시전이 순환합니다: {string.Join(" → ", path)}");
                }
                visiting.Add(id);
                foreach (int child in children[id]) { Visit(child); }
                visiting.RemoveAt(visiting.Count - 1);
                done.Add(id);
            }
        }

        // 자식 전용 강화(target)는 자식을 시전하는 카드를 먼저(또는 함께) 얻은 경우에만 적용되고,
        // 자식의 공격 종류가 소비하는 효과여야 한다. 위반하면 카드를 얻어도 효과가 조용히 사라지거나 강화가 실패한다.
        private static void ValidateChildOverlays(List<WeaponData> weapons)
        {
            var byId = new Dictionary<int, WeaponData>();
            foreach (var weapon in weapons) { byId[weapon.id] = weapon; }
            foreach (var weapon in weapons)
            {
                var cardsById = new Dictionary<string, WeaponUpgradeOption>();
                foreach (var option in weapon.upgrades) { cardsById[option.id] = option; }
                foreach (var option in weapon.upgrades)
                {
                    foreach (var effects in EffectLists(option))
                    {
                        foreach (var effect in effects)
                        {
                            if (effect.target == 0) { continue; }
                            string where = $"카드 '{option.id}'({weapon.name})의 효과 {effect.kind}";
                            if (!byId.TryGetValue(effect.target, out var child) || effect.target == weapon.id)
                            {
                                throw new InvalidOperationException($"{where}: 대상 자식 스킬 {effect.target}가 없거나 자기 자신입니다.");
                            }
                            if (!EffectRegistry.IsStatKind(effect.kind) || !EffectRegistry.Supports(child.castType, effect.kind))
                            {
                                throw new InvalidOperationException($"{where}: 자식 스킬 {child.name}({child.castType})이 소비하지 않는 효과입니다.");
                            }
                            if (!LinksChild(option, effect.target, cardsById, new HashSet<string>()))
                            {
                                throw new InvalidOperationException($"{where}: 자식 스킬 {child.name}을 시전하는 선행 카드가 없습니다.");
                            }
                        }
                    }
                }
            }
        }

        private static IEnumerable<List<EffectDef>> EffectLists(WeaponUpgradeOption option)
        {
            yield return option.effects;
            if (option.variants == null) { yield break; }
            foreach (var variant in option.variants) { yield return variant.effects; }
        }

        // 카드 자신이나 선행 카드(requiredCardIds, requiredCardCounts)를 거슬러 올라가 자식을 시전하는 효과가 있는지 본다.
        private static bool LinksChild(WeaponUpgradeOption option, int childId, Dictionary<string, WeaponUpgradeOption> cards, HashSet<string> seen)
        {
            if (!seen.Add(option.id)) { return false; }
            foreach (var effects in EffectLists(option))
            {
                foreach (var effect in effects)
                {
                    if (EffectRegistry.IsChildCast(effect.kind) && effect.skillId == childId) { return true; }
                }
            }
            var required = new List<string>();
            if (option.requiredCardIds != null) { required.AddRange(option.requiredCardIds); }
            if (option.requiredCardCounts != null)
            {
                foreach (var r in option.requiredCardCounts) { if (r != null && r.weaponId == 0) { required.Add(r.cardId); } }
            }
            foreach (var id in required)
            {
                if (cards.TryGetValue(id, out var previous) && LinksChild(previous, childId, cards, seen)) { return true; }
            }
            return false;
        }

        private static void CollectChildren(WeaponData weapon, WeaponUpgradeOption option, List<EffectDef> effects,
            HashSet<int> ids, List<int> children)
        {
            foreach (var effect in effects)
            {
                if (!EffectRegistry.IsChildCast(effect.kind)) { continue; }
                if (!ids.Contains(effect.skillId))
                {
                    throw new InvalidOperationException(
                        $"카드 '{option.id}'({weapon.name})의 효과 {effect.kind}: 자식 스킬 {effect.skillId}가 카탈로그에 없습니다.");
                }
                if (!ChildLink.TryParseInherit(effect.inherit, out _))
                {
                    throw new InvalidOperationException(
                        $"카드 '{option.id}'({weapon.name})의 효과 {effect.kind}: inherit에 알 수 없는 스탯 이름이 있거나 배율이 0 이하입니다.");
                }
                children.Add(effect.skillId);
            }
        }

        // 공격이 소비하지 않는 효과는 적용돼도 아무 일이 없으므로, 어느 카드가 문제인지 알려 주며 막는다.
        private static void ValidateEffectCompatibility(WeaponData weapon, WeaponUpgradeOption option)
        {
            Check(weapon, option, option.effects);
            if (option.variants == null) { return; }
            foreach (var variant in option.variants) { Check(weapon, option, variant.effects); }
        }

        private static void Check(WeaponData weapon, WeaponUpgradeOption option, List<EffectDef> effects)
        {
            foreach (var effect in effects)
            {
                if (EffectRegistry.Supports(weapon.castType, effect.kind)) { continue; }
                string reason = EffectRegistry.IsRegistered(effect.kind)
                    ? $"{weapon.castType} 공격이 쓰지 않는 효과입니다."
                    : "EffectRegistry에 등록되지 않은 효과 종류입니다.";
                throw new InvalidOperationException($"카드 '{option.id}'({weapon.name})의 효과 {effect.kind}: {reason}");
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
