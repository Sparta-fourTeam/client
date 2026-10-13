using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>표시 문구(이름·설명)의 클라이언트 전용 표. 서버 테이블에는 id와 수치만 있고, 문구는 같은 id로 여기서 채운다.
    /// 로컬라이징을 도입하면 이 표를 id 기반 문자열 키 조회로 바꾼다. 키는 스킬 id, 카드 id, 변형은 "카드id#minPermanentLevel", 아이템·일반 카드는 Id다</summary>
    [Serializable]
    public sealed class DisplayTexts
    {
        [Serializable]
        public sealed class Entry
        {
            public string name;
            public string desc;
            public string disabledReason;
        }

        public Dictionary<string, Entry> skills = new();
        public Dictionary<string, Entry> cards = new();
        public Dictionary<string, Entry> variants = new();
        public Dictionary<string, string> items = new();
        public Dictionary<string, Entry> generalCards = new();

        /// <summary>스킬, 강화 카드, 변형에 이름·설명을 채운다. 이름이 없는 스킬·카드는 데이터 오류로 거부한다</summary>
        public void ApplyTo(IEnumerable<SkillData> skillRows)
        {
            foreach (var skill in skillRows)
            {
                Fill(skills, skill.id.ToString(), $"Skills {skill.id}", out skill.name, out skill.desc, out _);
                foreach (var card in skill.upgrades ?? new List<SkillUpgradeOption>())
                {
                    Fill(cards, card.id, $"Skills {skill.id} 카드 {card.id}", out card.name, out card.desc, out card.disabledReason);
                    foreach (var variant in card.variants ?? Array.Empty<SkillUpgradeVariant>())
                    {
                        Fill(variants, $"{card.id}#{variant.minPermanentLevel}", $"카드 {card.id} 변형", out variant.name, out variant.desc, out _);
                    }
                }
            }
        }

        public void ApplyTo(IEnumerable<ItemDefinition> itemRows)
        {
            foreach (var item in itemRows)
            {
                item.Name = items.TryGetValue(item.Id ?? "", out var name) ? name : null;
            }
        }

        public void ApplyTo(IEnumerable<GeneralCardDefinition> cardRows)
        {
            foreach (var card in cardRows)
            {
                Fill(generalCards, card.Id ?? "", $"GeneralCards {card.Id}", out card.Name, out card.Desc, out _);
            }
        }

        private static void Fill(Dictionary<string, Entry> table, string key, string label, out string name, out string desc, out string disabledReason)
        {
            if (!table.TryGetValue(key, out var entry) || string.IsNullOrWhiteSpace(entry.name))
            {
                throw new InvalidOperationException($"DisplayTexts: {label}의 이름이 없습니다");
            }

            name = entry.name;
            desc = entry.desc;
            disabledReason = entry.disabledReason;
        }
    }
}
