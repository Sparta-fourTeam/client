using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;

namespace Game.Sandbox
{
    /// <summary>스킬 샌드박스의 조작 로직. 켠 스킬 여러 개와 카드를 얻은 순서로 구성(<see cref="SandboxBuild"/>)을 만들고,
    /// 구성이 바뀔 때마다 보유 스킬을 비우고 처음부터 다시 쌓는다. 같은 순서면 같은 결과이므로 카드 획득 순서 의존 문제도 그대로 드러난다.
    /// 카드는 게임과 같은 선행·배타 조건(<see cref="UpgradeEligibility"/>)으로 고를 수 있고, 조건 무시를 켜면 조건만 건너뛴다. 영구 레벨 조건은 표시만 하고 막지 않는다.</summary>
    public sealed class SkillSandboxSession
    {
        /// <summary>게임 HUD(SkillHud 프리팹)의 슬롯 수와 같다. 넘는 스킬은 HUD에 보이지 않는다</summary>
        public const int MaxSkills = 5;

        public readonly struct SkillInfo
        {
            public int Id { get; }
            public string Name { get; }
            public string AssetKey { get; }
            public CastType CastType { get; }
            public bool HasPrefab { get; }
            public SkillInfo(SkillData data, bool hasPrefab)
            {
                Id = data.id;
                Name = data.name;
                AssetKey = data.assetKey;
                CastType = data.castType;
                HasPrefab = hasPrefab;
            }
        }

        public sealed class CardInfo
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
            public int MaxPick { get; set; }
            public int Count { get; set; }
            public bool Enabled { get; set; }
            public string DisabledReason { get; set; }
            public bool Shared { get; set; }
            /// <summary>고르면 스킬의 형태가 바뀌는 형태 변환 카드</summary>
            public bool IsForm { get; set; }
            /// <summary>지금 고를 수 있으면 null, 없으면 이유</summary>
            public string Blocked { get; set; }
            /// <summary>선행·배타·영구 레벨·필요 스킬 조건과 지금 충족 여부. 조건이 없으면 비어 있다</summary>
            public List<string> Conditions { get; set; } = new List<string>();
        }

        private readonly SkillController controller;
        private readonly SandboxProgression progression;
        private readonly Dictionary<int, SkillData> catalog;

        public SandboxBuild Build { get; private set; } = new SandboxBuild();
        public IReadOnlyList<SkillInfo> Skills { get; }
        /// <summary>마지막 구성 적용에서 생긴 문제(적용하지 못한 카드 등). 문제가 없으면 null</summary>
        public string LastError { get; private set; }
        public event Action Changed;

        public SkillSandboxSession(SkillController controller, ISkillDataProvider provider, SandboxProgression progression)
        {
            this.controller = controller;
            this.progression = progression;
            var all = provider.LoadAll();
            catalog = all.ToDictionary(w => w.id);
            Skills = all.Where(w => !w.childOnly).OrderBy(w => w.id).Select(w => new SkillInfo(w, controller.HasPrefab(w.id))).ToList();
        }

        /// <summary>켠 스킬 (켠 순서)</summary>
        public IReadOnlyList<SkillBase> ActiveSkills => controller.Skills;

        // ── 스킬 ──────────────────────────────────────────────────────

        public bool IsOn(int skillId) => Build.steps.Any(s => s.skillId == skillId && s.IsSkill);

        /// <summary>스킬을 켜거나 끈다. 끄면 그 스킬의 카드도 함께 빠진다.</summary>
        public bool ToggleSkill(int skillId)
        {
            if (IsOn(skillId))
            {
                Build.steps.RemoveAll(s => s.skillId == skillId);
                Rebuild();
                return true;
            }
            if (!catalog.TryGetValue(skillId, out var data) || data.childOnly) { return Fail($"알 수 없는 스킬 {skillId}"); }
            if (!controller.HasPrefab(skillId)) { return Fail($"{data.name}: SkillAssetTable에 프리팹이 없습니다."); }
            if (Build.steps.Count(s => s.IsSkill) >= MaxSkills) { return Fail($"스킬은 최대 {MaxSkills}개까지 켤 수 있습니다 (게임 HUD 슬롯 수)."); }
            Build.steps.Add(new SandboxStep { skillId = skillId });
            Rebuild();
            return LastError == null;
        }

        private bool Fail(string message)
        {
            LastError = message;
            Changed?.Invoke();
            return false;
        }

        // ── 카드 ──────────────────────────────────────────────────────

        /// <summary>켠 스킬 하나의 카드 목록. 조건을 못 채웠거나 비활성·최대 횟수인 카드는 Blocked에 이유가 있다.</summary>
        public List<CardInfo> Cards(int skillId)
        {
            var list = new List<CardInfo>();
            var weapon = FindWeapon(skillId);
            if (weapon == null) { return list; }
            foreach (var option in weapon.Data.upgrades)
            {
                list.Add(new CardInfo
                {
                    Id = option.id,
                    Name = option.name,
                    Description = option.desc,
                    MaxPick = option.maxPickCount,
                    Count = controller.GetAcquiredCount(skillId, option.id),
                    Enabled = option.enabled,
                    DisabledReason = option.disabledReason,
                    Shared = !string.IsNullOrEmpty(option.sharedId),
                    IsForm = FormHintFinder.TryGetForm(option, controller.GetPermanentWeaponLevel(skillId), out _),
                    Blocked = BlockedReason(weapon, option),
                    Conditions = Conditions(weapon, option)
                });
            }
            return list;
        }

        public bool TryAddCard(int skillId, string cardId, out string reason)
        {
            reason = null;
            var weapon = FindWeapon(skillId);
            var option = weapon?.Data.upgrades.Find(c => c.id == cardId);
            if (option == null) { reason = "이 스킬에 없는 카드"; return false; }
            reason = BlockedReason(weapon, option);
            if (reason != null) { return false; }
            Build.steps.Add(new SandboxStep { skillId = skillId, cardId = cardId });
            Rebuild();
            if (LastError != null) { reason = LastError; return false; }
            return true;
        }

        /// <summary>마지막으로 얻은 해당 카드를 하나 뺀다. 이 카드가 선행인 카드는 다시 쌓을 때 함께 빠진다.</summary>
        public bool RemoveCard(int skillId, string cardId)
        {
            int index = Build.steps.FindLastIndex(s => s.skillId == skillId && s.cardId == cardId);
            if (index < 0) { return false; }
            Build.steps.RemoveAt(index);
            Rebuild();
            return true;
        }

        /// <summary>카드를 모두 비운다. 켠 스킬은 그대로 둔다.</summary>
        public void ClearCards()
        {
            Build.steps.RemoveAll(s => !s.IsSkill);
            Rebuild();
        }

        public void SetIgnoreConditions(bool ignore)
        {
            Build.ignoreConditions = ignore;
            Rebuild();
        }

        public void SetPermanentLevel(int level)
        {
            Build.permanentLevel = Math.Max(0, level);
            Rebuild();
        }

        private SkillBase FindWeapon(int skillId) => controller.Skills.FirstOrDefault(w => w.Data.id == skillId);

        private string BlockedReason(SkillBase weapon, SkillUpgradeOption option)
        {
            int id = weapon.Data.id;
            if (!option.enabled) { return option.disabledReason ?? "비활성 카드"; }
            if (controller.GetAcquiredCount(id, option.id) >= option.maxPickCount) { return "최대 횟수"; }
            // 영구 레벨 조건은 표시만 하고 막지 않는다(샌드박스는 영구 레벨을 자유롭게 바꿔 시험하는 곳이다)
            if (!Build.ignoreConditions && !UpgradeEligibility.CanAcquire(option, id, controller, ignorePermanentLevel: true)) { return "조건 미충족"; }
            if (!SkillUpgradeTransaction.CanApply(weapon, option, controller.Skills, controller.GetPermanentWeaponLevel(id)))
            {
                return "함께 강화할 스킬이 없거나 적용할 수 없다";
            }
            return null;
        }

        // ── 조건 안내 ─────────────────────────────────────────────────

        private List<string> Conditions(SkillBase weapon, SkillUpgradeOption option)
        {
            var lines = new List<string>();
            int id = weapon.Data.id;
            if (option.minPermanentLevel > 0)
            {
                lines.Add($"영구 Lv {option.minPermanentLevel} 이상");
            }
            if (option.minBattleLevel > 1)
            {
                lines.Add($"전투 Lv {option.minBattleLevel} 이상 {Mark(weapon.Level >= option.minBattleLevel)}");
            }
            if (option.requiredWeaponIds != null)
            {
                foreach (int required in option.requiredWeaponIds)
                {
                    lines.Add($"스킬 {SkillName(required)} 보유 {Mark(controller.GetWeaponLevel(required) > 0)}");
                }
            }
            if (option.requiredCardCounts != null)
            {
                foreach (var requirement in option.requiredCardCounts)
                {
                    int owner = requirement.skillId == 0 ? id : requirement.skillId;
                    string count = requirement.count > 1 ? $" x{requirement.count}" : string.Empty;
                    lines.Add($"선행 {CardName(owner, requirement.cardId, id)}{count} {Mark(controller.GetAcquiredCount(owner, requirement.cardId) >= requirement.count)}");
                }
            }
            if (option.exclusions != null)
            {
                foreach (var exclusion in option.exclusions)
                {
                    int owner = UpgradeEligibility.ExclusionOwner(exclusion, id);
                    string until = exclusion.belowPermanentLevel > 0 ? $" (영구 Lv {exclusion.belowPermanentLevel} 미만에서만)" : string.Empty;
                    bool applies = UpgradeEligibility.ExclusionApplies(exclusion, id, controller);
                    bool clear = !applies || controller.GetAcquiredCount(owner, exclusion.cardId) == 0;
                    lines.Add($"배타 {CardName(owner, exclusion.cardId, id)}{until} {(clear ? "(충족)" : "(보유 중)")}");
                }
            }
            return lines;
        }

        private static string Mark(bool met) => met ? "(충족)" : "(미충족)";

        private string SkillName(int skillId) => catalog.TryGetValue(skillId, out var data) ? data.name : skillId.ToString();

        private string CardName(int ownerId, string cardId, int selfId)
        {
            string name = catalog.TryGetValue(ownerId, out var data) ? data.upgrades?.Find(c => c.id == cardId)?.name ?? cardId : cardId;
            return ownerId == selfId ? name : $"{SkillName(ownerId)}의 {name}";
        }

        // ── 구성 적용 ─────────────────────────────────────────────────

        /// <summary>보유 스킬을 비우고 구성대로 처음부터 다시 쌓는다. 적용하지 못한 단계는 구성에서 빼고 LastError에 남긴다.</summary>
        public void Rebuild()
        {
            LastError = null;
            progression.Level = Build.permanentLevel;
            controller.RefreshPermanentLevels();
            controller.ClearWeapons();
            var rejected = new List<string>();
            foreach (var step in Build.steps.ToList())
            {
                if (!Apply(step, out string label)) { rejected.Add(label); Build.steps.Remove(step); }
            }
            if (rejected.Count > 0) { LastError = "적용하지 못한 항목: " + string.Join(", ", rejected); }
            Changed?.Invoke();
        }

        private bool Apply(SandboxStep step, out string label)
        {
            label = SkillName(step.skillId);
            if (step.IsSkill) { return controller.AddWeapon(step.skillId); }
            var weapon = FindWeapon(step.skillId);
            var option = weapon?.Data.upgrades.Find(c => c.id == step.cardId);
            label = $"{label}/{option?.name ?? step.cardId}";
            return option != null && BlockedReason(weapon, option) == null
                && SkillUpgradeTransaction.TryApply(weapon, option, controller.Skills, controller.GetPermanentWeaponLevel(step.skillId));
        }

        // ── 구성 불러오기·저장 ────────────────────────────────────────

        public bool LoadBuild(SandboxBuild build)
        {
            if (build == null) { return false; }
            Build = build.Clone();
            Rebuild();
            return LastError == null;
        }

        public void SavePreset(string slot) => SandboxPresets.Save(slot, Build);

        public bool LoadPreset(string slot) => SandboxPresets.TryLoad(slot, out var build) && LoadBuild(build);

        public string StatsText() => controller.Skills.Count == 0
            ? SandboxStatsText.Describe(null)
            : string.Join("\n\n", controller.Skills.Select(SandboxStatsText.Describe));
    }
}
