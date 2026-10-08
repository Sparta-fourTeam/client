using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>공유 강화의 모든 효과를 준비한 뒤 상태를 함께 갱신한다.</summary>
    public static class SkillUpgradeTransaction
    {
        private sealed class Prepared
        {
            public SkillBase skill;
            public SkillUpgradeOption Option;
            public SkillConfig Config;
        }

        public static bool CanApply(SkillBase owner, SkillUpgradeOption option, IReadOnlyList<SkillBase> weapons, int permanentLevel)
        {
            return TryPrepare(owner, option, weapons, permanentLevel, out _);
        }

        public static bool TryApply(SkillBase owner, SkillUpgradeOption option, IReadOnlyList<SkillBase> weapons, int permanentLevel)
        {
            if (!TryPrepare(owner, option, weapons, permanentLevel, out var prepared)) { return false; }
            foreach (var item in prepared) { item.skill.CommitUpgrade(item.Option, item.Config); }
            return true;
        }

        private static bool TryPrepare(SkillBase owner, SkillUpgradeOption option, IReadOnlyList<SkillBase> weapons,
            int permanentLevel, out List<Prepared> prepared)
        {
            prepared = new List<Prepared>();
            if (owner == null || option == null || weapons == null
                || owner.Data.upgrades == null || !owner.Data.upgrades.Contains(option)
                || !SkillUpgradeResolver.TryResolve(option, permanentLevel, out var resolved)) { return false; }
            bool shared = !string.IsNullOrEmpty(option.sharedId);
            var participants = shared ? option.affectedWeaponIds : new[] { owner.Data.id };
            if (participants == null || (shared && participants.Length < 2)) { return false; }
            var ids = new HashSet<int>();
            bool containsOwner = false;
            foreach (int id in participants)
            {
                if (!ids.Add(id)) { return false; }
                SkillBase skill = null;
                foreach (var candidate in weapons)
                {
                    if (candidate.Data.id != id) { continue; }
                    if (skill != null) { return false; }
                    skill = candidate;
                }
                if (skill == null) { return false; }
                containsOwner |= ReferenceEquals(skill, owner);
                var member = shared ? skill.Data.upgrades?.Find(o => o.sharedId == option.sharedId) : option;
                if (member == null || (shared && !SameSharedDefinition(option, member))
                    || !skill.TryPrepareUpgrade(member, resolved.Effects, out var stats)) { return false; }
                prepared.Add(new Prepared { skill = skill, Option = member, Config = stats });
            }
            return containsOwner;
        }

        private static bool SameSharedDefinition(SkillUpgradeOption a, SkillUpgradeOption b)
        {
            if (a.sharedId != b.sharedId || a.maxPickCount != b.maxPickCount || a.variants?.Length > 0 || b.variants?.Length > 0
                || a.affectedWeaponIds == null || b.affectedWeaponIds == null
                || a.affectedWeaponIds.Length != b.affectedWeaponIds.Length || a.effects == null || b.effects == null
                || a.effects.Count != b.effects.Count) { return false; }
            var ids = new HashSet<int>(a.affectedWeaponIds);
            if (ids.Count != a.affectedWeaponIds.Length || !ids.SetEquals(b.affectedWeaponIds)) { return false; }
            for (int i = 0; i < a.effects.Count; i++)
            {
                if (a.effects[i] == null || b.effects[i] == null
                    || a.effects[i].kind != b.effects[i].kind || a.effects[i].value != b.effects[i].value) { return false; }
            }
            return true;
        }

        /// <summary>sharedId가 같은 카드를 가진 스킬을 모아 각 카드의 affectedWeaponIds를 채운다.
        /// 데이터에는 대상 목록을 적지 않으므로, 카탈로그를 읽은 직후 검증보다 먼저 호출한다</summary>
        public static void LinkSharedTargets(List<SkillData> weapons)
        {
            // 비었거나 잘못된 정의는 여기서 건너뛰고 검증이 정해진 예외로 거절한다
            if (weapons == null) { return; }
            var groups = new Dictionary<string, List<int>>();
            foreach (var weapon in weapons)
            {
                foreach (var option in OptionsOf(weapon))
                {
                    if (string.IsNullOrEmpty(option.sharedId)) { continue; }
                    if (!groups.TryGetValue(option.sharedId, out var ids)) { groups[option.sharedId] = ids = new List<int>(); }
                    ids.Add(weapon.id);
                }
            }
            foreach (var weapon in weapons)
            {
                foreach (var option in OptionsOf(weapon))
                {
                    option.affectedWeaponIds = string.IsNullOrEmpty(option.sharedId) ? null : groups[option.sharedId].ToArray();
                }
            }
        }

        private static IEnumerable<SkillUpgradeOption> OptionsOf(SkillData weapon)
        {
            if (weapon?.upgrades == null) { yield break; }
            foreach (var option in weapon.upgrades)
            {
                if (option != null) { yield return option; }
            }
        }

        public static void ValidateCatalog(List<SkillData> weapons)
        {
            var ids = new Dictionary<int, SkillData>();
            foreach (var weapon in weapons) { ids.Add(weapon.id, weapon); }
            foreach (var weapon in weapons)
            {
                var localGroups = new HashSet<string>();
                foreach (var option in weapon.upgrades)
                {
                    if (string.IsNullOrEmpty(option.sharedId))
                    {
                        if (option.affectedWeaponIds != null) { throw new InvalidOperationException("공유 키 없는 대상 스킬 목록입니다."); }
                        continue;
                    }
                    if (!localGroups.Add(option.sharedId) || option.affectedWeaponIds == null
                        || option.affectedWeaponIds.Length < 2 || Array.IndexOf(option.affectedWeaponIds, weapon.id) < 0)
                    {
                        throw new InvalidOperationException("공유 강화 참여 스킬 정의가 잘못되었습니다.");
                    }
                    foreach (int id in option.affectedWeaponIds)
                    {
                        if (!ids.TryGetValue(id, out var target)) { throw new InvalidOperationException("공유 강화 스킬 참조가 없습니다."); }
                        var member = target.upgrades.Find(o => o.sharedId == option.sharedId);
                        if (member == null || !SameSharedDefinition(option, member))
                        {
                            throw new InvalidOperationException("공유 강화의 양쪽 횟수 또는 효과가 다릅니다.");
                        }
                    }
                }
            }
        }
    }
}
