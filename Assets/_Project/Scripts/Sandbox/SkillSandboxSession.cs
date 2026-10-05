using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;

namespace Game.Sandbox
{
    /// <summary>스킬 샌드박스의 조작 로직 (UI와 분리해 테스트한다). 스킬 하나와 카드 목록으로 구성(<see cref="SandboxBuild"/>)을 만들고,
    /// 구성이 바뀔 때마다 보유 스킬을 비우고 처음부터 다시 쌓는다. 같은 순서면 같은 결과이므로 카드 획득 순서 의존 문제도 그대로 드러난다.</summary>
    public sealed class SkillSandboxSession
    {
        public readonly struct SkillInfo
        {
            public int Id { get; }
            public string Name { get; }
            public string IconKey { get; }
            public CastType CastType { get; }
            public bool HasPrefab { get; }
            public SkillInfo(SkillData data, bool hasPrefab)
            {
                Id = data.id;
                Name = data.name;
                IconKey = data.iconKey;
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
            /// <summary>직접 적용할 수 있으면 null, 없으면 이유</summary>
            public string Blocked { get; set; }
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

        public SkillBase Current => controller.Skills.FirstOrDefault(w => w.Data.id == Build.skillId);

        // ── 스킬과 카드 ───────────────────────────────────────────────

        public bool SelectSkill(int skillId)
        {
            if (!catalog.TryGetValue(skillId, out var data) || data.childOnly) { LastError = $"알 수 없는 스킬 {skillId}"; return false; }
            if (!controller.HasPrefab(skillId)) { LastError = $"{data.name}: 프리팹 연결(prefabEntries)이 없습니다."; Changed?.Invoke(); return false; }
            Build = new SandboxBuild { skillId = skillId, permanentLevel = Build.permanentLevel };
            Rebuild();
            return LastError == null;
        }

        /// <summary>선택한 스킬의 카드 목록. 선행 조건은 보지 않고, 비활성 카드·공유 카드·최대 횟수만 막는다.</summary>
        public List<CardInfo> Cards()
        {
            var list = new List<CardInfo>();
            if (!catalog.TryGetValue(Build.skillId, out var data)) { return list; }
            foreach (var option in data.upgrades)
            {
                int count = Build.cards.Count(c => c == option.id);
                var info = new CardInfo
                {
                    Id = option.id,
                    Name = option.name,
                    Description = option.desc,
                    MaxPick = option.maxPickCount,
                    Count = count,
                    Enabled = option.enabled,
                    DisabledReason = option.disabledReason,
                    Shared = !string.IsNullOrEmpty(option.sharedId)
                };
                info.Blocked = !option.enabled ? (option.disabledReason ?? "비활성 카드")
                    : info.Shared ? "공유 카드는 실제 선택(3지선다) 모드에서만 적용할 수 있다"
                    : count >= option.maxPickCount ? "최대 횟수"
                    : null;
                list.Add(info);
            }
            return list;
        }

        public bool TryAddCard(string cardId, out string reason)
        {
            reason = null;
            var card = Cards().Find(c => c.Id == cardId);
            if (card == null) { reason = "이 스킬에 없는 카드"; return false; }
            if (card.Blocked != null) { reason = card.Blocked; return false; }
            Build.cards.Add(cardId);
            Rebuild();
            if (LastError != null) { reason = LastError; return false; }
            return true;
        }

        /// <summary>마지막으로 얻은 해당 카드를 하나 뺀다.</summary>
        public bool RemoveCard(string cardId)
        {
            int index = Build.cards.LastIndexOf(cardId);
            if (index < 0) { return false; }
            Build.cards.RemoveAt(index);
            Rebuild();
            return true;
        }

        public void ClearCards()
        {
            Build.cards.Clear();
            Rebuild();
        }

        public void SetPermanentLevel(int level)
        {
            Build.permanentLevel = Math.Max(0, level);
            Rebuild();
        }

        // ── 실제 3지선다 ──────────────────────────────────────────────

        /// <summary>게임과 같은 규칙(선행 조건, 최대 횟수, 제외, 영구 레벨)으로 지금 고를 수 있는 현재 스킬의 강화 카드를 최대 count장 뽑는다.</summary>
        public List<UpgradeChoice> RollChoices(int count = 3)
        {
            var result = new List<UpgradeChoice>();
            if (Current == null) { return result; }
            foreach (var choice in controller.GetRandomUpgradeChoices(200))
            {
                if (choice.IsNewWeapon || choice.skill == null || choice.skill.Data.id != Build.skillId) { continue; }
                result.Add(choice);
                if (result.Count >= count) { break; }
            }
            return result;
        }

        public bool Pick(UpgradeChoice choice)
        {
            if (choice.Option == null || !controller.ApplyUpgradeChoice(choice)) { return false; }
            Build.cards.Add(choice.Option.id);
            Changed?.Invoke();
            return true;
        }

        // ── 구성 적용 ─────────────────────────────────────────────────

        /// <summary>보유 스킬을 비우고 구성대로 처음부터 다시 쌓는다. 적용하지 못한 카드는 구성에서 빼고 LastError에 남긴다.</summary>
        public void Rebuild()
        {
            LastError = null;
            progression.Level = Build.permanentLevel;
            controller.RefreshPermanentLevels();
            controller.ClearWeapons();
            if (Build.skillId > 0)
            {
                if (!controller.AddWeapon(Build.skillId)) { LastError = $"스킬 {Build.skillId}를 얻지 못했습니다."; }
                else { ApplyCards(); }
            }
            Changed?.Invoke();
        }

        private void ApplyCards()
        {
            var weapon = Current;
            var rejected = new List<string>();
            foreach (var cardId in Build.cards.ToList())
            {
                var option = weapon.Data.upgrades.Find(c => c.id == cardId);
                if (option == null || !weapon.LevelUp(option, Build.permanentLevel))
                {
                    rejected.Add(cardId);
                    Build.cards.Remove(cardId);
                }
            }
            if (rejected.Count > 0) { LastError = "적용하지 못한 카드: " + string.Join(", ", rejected); }
        }

        // ── 구성 불러오기·저장 ────────────────────────────────────────

        public bool LoadBuild(SandboxBuild build)
        {
            if (build == null || !SelectSkill(build.skillId)) { return false; }
            Build = build.Clone();
            Rebuild();
            return LastError == null;
        }

        public void SavePreset(string slot) => SandboxPresets.Save(slot, Build);

        public bool LoadPreset(string slot) => SandboxPresets.TryLoad(slot, out var build) && LoadBuild(build);

        public string StatsText() => SandboxStatsText.Describe(Current);
    }
}
