using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>Validates catalog identities, shared upgrades, and the prerequisite graph.</summary>
    public static class SkillCatalogValidator
    {
        public static void Validate(List<SkillData> weapons)
        {
            if (weapons == null || weapons.Count == 0)
            {
                throw new InvalidOperationException("무기 카탈로그가 비어 있습니다.");
            }

            // 공유 강화의 대상 스킬은 데이터에 적지 않으므로 검증 전에 sharedId로 모아 채운다
            SkillUpgradeTransaction.LinkSharedTargets(weapons);

            var ids = new HashSet<int>();
            var assetKeys = new HashSet<string>();
            var cards = new HashSet<string>();
            foreach (var weapon in weapons)
            {
                if (weapon == null || !ids.Add(weapon.id) || weapon.baseStats == null
                    || weapon.maxLevel < 1 || weapon.upgrades == null || weapon.unlockLevel < 1)
                {
                    throw new InvalidOperationException("무기 정의 또는 ID가 잘못되었습니다.");
                }
                if (string.IsNullOrEmpty(weapon.assetKey) || !assetKeys.Add(weapon.assetKey))
                {
                    throw new InvalidOperationException($"스킬 '{weapon.name}'의 assetKey가 비어 있거나 다른 스킬과 겹칩니다. 프리팹과 아이콘을 찾는 키라 스킬마다 유일해야 합니다.");
                }
                if (!SkillFactory.IsRegistered(weapon.castType))
                {
                    throw new InvalidOperationException($"무기 '{weapon.name}'의 공격 종류 {weapon.castType}에 등록된 공격 전략이 없습니다.");
                }
                if (!Enum.IsDefined(typeof(Element), weapon.element))
                {
                    throw new InvalidOperationException($"스킬 '{weapon.name}'의 속성 {(int)weapon.element}이(가) 올바르지 않습니다 (0~5).");
                }

                foreach (var option in weapon.upgrades)
                {
                    if (option == null || string.IsNullOrEmpty(option.id) || !cards.Add(option.id)
                        || option.maxPickCount <= 0 || !SkillUpgradeResolver.TryResolve(option, 0, out _))
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
            SkillUpgradeTransaction.ValidateCatalog(weapons);
            ValidateConditions(weapons);
            ValidateChildSkills(weapons);
        }

        // 자식 스킬 시전과 보관 효과(target)는 존재하는 스킬만 가리켜야 하고, 한 스킬 아래에서 시전이 순환하면 안 된다.
        // 시전 관계는 스킬마다 따로 본다: 자기 효과(onEvent)와 target으로 후손에게 붙인 효과가 같은 시전 트리를 이룬다.
        private static void ValidateChildSkills(List<SkillData> weapons)
        {
            var byId = new Dictionary<int, SkillData>();
            foreach (var weapon in weapons) { byId[weapon.id] = weapon; }
            var global = new Dictionary<int, List<int>>();
            foreach (var weapon in weapons)
            {
                var edges = new Dictionary<int, List<int>>();
                foreach (var option in weapon.upgrades)
                {
                    foreach (var effects in EffectLists(option))
                    {
                        foreach (var effect in effects)
                        {
                            string where = $"카드 '{option.id}'({weapon.name})의 효과 {effect.kind}";
                            ValidateChildEffect(weapon, effect, where, byId);
                            if (!EffectRegistry.IsChildCast(effect.kind)) { continue; }
                            int source = effect.target == 0 ? weapon.id : effect.target;
                            if (!edges.TryGetValue(source, out var list)) { edges[source] = list = new List<int>(); }
                            list.Add(effect.skillId);
                            if (effect.target == 0)
                            {
                                if (!global.TryGetValue(weapon.id, out var direct)) { global[weapon.id] = direct = new List<int>(); }
                                direct.Add(effect.skillId);
                            }
                        }
                    }
                }
                ValidateTree(weapon, edges);
            }
            // 스킬끼리 서로를 자식으로 시전하는 순환은 스킬이 고유 효과를 갖게 되면 실제 순환이 되므로 미리 막는다.
            DetectCycle(global, new List<int>(global.Keys));
        }

        private static void DetectCycle(Dictionary<int, List<int>> edges, IEnumerable<int> starts)
        {
            var visiting = new List<int>();
            var done = new HashSet<int>();
            foreach (var start in starts) { Visit(start); }
            void Visit(int id)
            {
                if (done.Contains(id)) { return; }
                if (visiting.Contains(id))
                {
                    int first = visiting.IndexOf(id);
                    var path = visiting.GetRange(first, visiting.Count - first);
                    path.Add(id);
                    throw new InvalidOperationException($"자식 스킬 시전이 순환합니다: {string.Join(" → ", path)}");
                }
                visiting.Add(id);
                if (edges.TryGetValue(id, out var next)) { foreach (int child in next) { Visit(child); } }
                visiting.RemoveAt(visiting.Count - 1);
                done.Add(id);
            }
        }

        private static void ValidateChildEffect(SkillData skill, EffectDef effect, string where, Dictionary<int, SkillData> byId)
        {
            if (EffectRegistry.IsChildCast(effect.kind) || effect.kind == "inherit")
            {
                if (!byId.ContainsKey(effect.skillId))
                {
                    throw new InvalidOperationException($"{where}: 자식 스킬 {effect.skillId}가 카탈로그에 없습니다.");
                }
                if (!ChildLink.TryParseInherit(effect.inherit, out _) || effect.kind == "inherit" && (effect.inherit == null || effect.inherit.Count == 0))
                {
                    throw new InvalidOperationException($"{where}: inherit에 알 수 없는 스탯 이름이 있거나 배율이 0 이하이거나 비어 있습니다.");
                }
            }
            if (effect.target == 0) { return; }
            if (!byId.TryGetValue(effect.target, out var target) || effect.target == skill.id)
            {
                throw new InvalidOperationException($"{where}: 대상 스킬 {effect.target}가 없거나 자기 자신입니다.");
            }
            if (EffectRegistry.IsStatKind(effect.kind) && !EffectRegistry.Supports(target.castType, effect.kind))
            {
                throw new InvalidOperationException($"{where}: 스킬 {target.name}({target.castType})이 소비하지 않는 효과입니다.");
            }
        }

        // 순환과, 보관 효과의 대상이 이 스킬 아래에서 실제로 시전될 수 있는지를 확인한다.
        private static void ValidateTree(SkillData skill, Dictionary<int, List<int>> edges)
        {
            DetectCycle(edges, new List<int>(edges.Keys) { skill.id });

            var reachable = new HashSet<int> { skill.id };
            var queue = new Queue<int>();
            queue.Enqueue(skill.id);
            while (queue.Count > 0)
            {
                if (!edges.TryGetValue(queue.Dequeue(), out var next)) { continue; }
                foreach (int child in next) { if (reachable.Add(child)) { queue.Enqueue(child); } }
            }
            foreach (var option in skill.upgrades)
            {
                foreach (var effects in EffectLists(option))
                {
                    foreach (var effect in effects)
                    {
                        if (effect.target != 0 && !reachable.Contains(effect.target))
                        {
                            throw new InvalidOperationException(
                                $"카드 '{option.id}'({skill.name})의 효과 {effect.kind}: 스킬 {effect.target}는 {skill.name} 아래에서 시전되는 카드가 없어 효과가 쓰이지 않습니다.");
                        }
                    }
                }
            }
        }

        private static IEnumerable<List<EffectDef>> EffectLists(SkillUpgradeOption option)
        {
            yield return option.effects;
            if (option.variants == null) { yield break; }
            foreach (var variant in option.variants) { yield return variant.effects; }
        }

        // 공격이 소비하지 않는 효과는 적용돼도 아무 일이 없으므로, 어느 카드가 문제인지 알려 주며 막는다.
        // target이 있는 효과는 대상 스킬이 소비하는지를 따로 본다(ValidateChildEffect).
        private static void ValidateEffectCompatibility(SkillData skill, SkillUpgradeOption option)
        {
            Check(skill, option, option.effects);
            if (option.variants == null) { return; }
            foreach (var variant in option.variants) { Check(skill, option, variant.effects); }
        }

        private static void Check(SkillData skill, SkillUpgradeOption option, List<EffectDef> effects)
        {
            foreach (var effect in effects)
            {
                if (effect.target != 0 && EffectRegistry.IsRegistered(effect.kind)) { continue; }
                if (EffectRegistry.Supports(skill.castType, effect.kind)) { continue; }
                string reason = EffectRegistry.IsRegistered(effect.kind)
                    ? $"{skill.castType} 공격이 쓰지 않는 효과입니다."
                    : "EffectRegistry에 등록되지 않은 효과 종류입니다.";
                throw new InvalidOperationException($"카드 '{option.id}'({skill.name})의 효과 {effect.kind}: {reason}");
            }
        }

        private static void ValidateConditions(List<SkillData> weapons)
        {
            var byId = new Dictionary<int, SkillData>();
            var owners = new Dictionary<string, int>();
            var options = new Dictionary<string, SkillUpgradeOption>();
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
                    if (option.requiredCardCounts != null)
                    {
                        foreach (var r in option.requiredCardCounts)
                        {
                            if (r == null) { Invalid(); }
                            Require(r.cardId, r.skillId == 0 ? weapon.id : r.skillId, r.count);
                        }
                    }
                    if (option.exclusions != null)
                    {
                        foreach (var e in option.exclusions)
                        {
                            if (e == null || e.belowPermanentLevel < 0 || string.IsNullOrEmpty(e.cardId)
                                || !owners.TryGetValue(e.cardId, out var owner)
                                || owner != (e.skillId == 0 ? weapon.id : e.skillId)) { Invalid(); }
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
