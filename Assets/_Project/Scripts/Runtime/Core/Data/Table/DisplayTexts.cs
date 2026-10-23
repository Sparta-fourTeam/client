using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Game.Core
{
    /// <summary>표시 문구(이름·설명)의 클라이언트 전용 표. 서버 테이블에는 id와 수치만 있고, 문구는 같은 id로 여기서 채운다.
    /// 로케일별 JSON 파일(ClientData/DisplayTexts.json이 ko 원본, DisplayTexts.&lt;코드&gt;.json이 번역)을 같은 구조로 둔다.
    /// 번역 파일에 항목이 없거나 값이 비어 있거나 파일이 없으면 ko 값을 쓴다.
    /// 키는 스킬 id, 카드 id, 변형은 "카드id#minPermanentLevel", 아이템·일반 카드는 Id다</summary>
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

        /// <summary>로케일의 표시 문구. ko 원본을 바탕으로 번역 파일의 비어 있지 않은 값만 덮어쓴다(항목 단위 폴백).
        /// 이름 검증(Fill)은 ko 값 기준이라 번역이 비어도 데이터 오류가 되지 않는다. 번역 파일이 없어도 예외 없이 ko를 돌려준다</summary>
        public static DisplayTexts Load(string localeCode)
        {
            var asset = Resources.Load<TextAsset>("ClientData/DisplayTexts")
                ?? throw new InvalidOperationException("Resources/ClientData/DisplayTexts.json을 찾을 수 없습니다");
            var result = JsonConvert.DeserializeObject<DisplayTexts>(asset.text);
            if (string.IsNullOrEmpty(localeCode) || localeCode == BaseLocale) { return result; }

            var translated = Resources.Load<TextAsset>($"ClientData/DisplayTexts.{localeCode}");
            if (translated == null) { return result; }
            var overlay = JsonConvert.DeserializeObject<DisplayTexts>(translated.text);
            if (overlay == null) { return result; }

            Overlay(result.skills, overlay.skills);
            Overlay(result.cards, overlay.cards);
            Overlay(result.variants, overlay.variants);
            Overlay(result.generalCards, overlay.generalCards);
            if (overlay.items != null)
            {
                foreach (var pair in overlay.items)
                {
                    if (result.items.ContainsKey(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value)) { result.items[pair.Key] = pair.Value; }
                }
            }

            return result;
        }

        /// <summary>원본 로케일 코드. 접미사 없는 파일이다</summary>
        public const string BaseLocale = "ko";

        private static void Overlay(Dictionary<string, Entry> target, Dictionary<string, Entry> source)
        {
            if (source == null) { return; }
            foreach (var pair in source)
            {
                if (pair.Value == null || !target.TryGetValue(pair.Key, out var entry)) { continue; }
                if (!string.IsNullOrWhiteSpace(pair.Value.name)) { entry.name = pair.Value.name; }
                if (!string.IsNullOrWhiteSpace(pair.Value.desc)) { entry.desc = pair.Value.desc; }
                if (!string.IsNullOrWhiteSpace(pair.Value.disabledReason)) { entry.disabledReason = pair.Value.disabledReason; }
            }
        }

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
