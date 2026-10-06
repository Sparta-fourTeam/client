using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>스킬 하나가 쓰는 에셋 묶음. 키는 Skills.json의 assetKey와 같다</summary>
    [Serializable]
    public class SkillAssetEntry
    {
        public string key;

        /// <summary>공격 실체 프리팹. 연결돼 있어야 스킬을 얻을 수 있다</summary>
        public GameObject prefab;

        /// <summary>HUD 슬롯 아이콘</summary>
        public Sprite hudIcon;

        /// <summary>카드 화면의 "새 스킬" 카드 아이콘</summary>
        public Sprite newCardIcon;

        /// <summary>카드 화면의 "강화" 카드 아이콘. 스킬이 아닌 일반 카드(GeneralCards.json의 IconKey)도 이 칸을 쓴다</summary>
        public Sprite upgradeCardIcon;
    }

    public enum SkillCardIcon { New, Upgrade }

    /// <summary>스킬별 에셋(프리팹, HUD 아이콘, 카드 아이콘)을 키 하나로 찾는 표. 새 스킬을 추가하면 이 표에 항목 하나만 더하면 된다.
    /// 아이콘이 없는 키는 기본 아이콘을 돌려준다</summary>
    [CreateAssetMenu(fileName = "SkillAssetTable", menuName = "Project Nova/Skill Asset Table")]
    public sealed class SkillAssetTable : ScriptableObject
    {
        [SerializeField] private List<SkillAssetEntry> entries = new List<SkillAssetEntry>();
        [SerializeField] private Sprite hudFallback;
        [SerializeField] private Sprite cardFallback;

        private Dictionary<string, SkillAssetEntry> lookup;

        public IReadOnlyList<SkillAssetEntry> Entries => entries;

        /// <summary>코드(테스트, 도구)에서 표를 만들 때 쓴다. 에셋 표는 인스펙터에서 채운다</summary>
        public static SkillAssetTable Create(IEnumerable<SkillAssetEntry> entries, Sprite hudFallback = null, Sprite cardFallback = null)
        {
            var table = CreateInstance<SkillAssetTable>();
            table.entries = new List<SkillAssetEntry>(entries);
            table.hudFallback = hudFallback;
            table.cardFallback = cardFallback;
            return table;
        }

        public bool TryGet(string key, out SkillAssetEntry entry)
        {
            if (lookup == null)
            {
                lookup = new Dictionary<string, SkillAssetEntry>(entries.Count);
                foreach (var candidate in entries)
                {
                    if (candidate != null && !string.IsNullOrEmpty(candidate.key)) { lookup[candidate.key] = candidate; }
                }
            }

            entry = null;
            return !string.IsNullOrEmpty(key) && lookup.TryGetValue(key, out entry);
        }

        public bool HasPrefab(string key) => TryGet(key, out var entry) && entry.prefab != null;

        public GameObject GetPrefab(string key) => TryGet(key, out var entry) ? entry.prefab : null;

        public Sprite GetHudIcon(string key) =>
            TryGet(key, out var entry) && entry.hudIcon != null ? entry.hudIcon : hudFallback;

        public Sprite GetCardIcon(string key, SkillCardIcon kind)
        {
            if (!TryGet(key, out var entry)) { return cardFallback; }
            var icon = kind == SkillCardIcon.New ? entry.newCardIcon : entry.upgradeCardIcon;
            return icon != null ? icon : cardFallback;
        }

        // 인스펙터에서 항목을 고치면 다음 조회에서 다시 만든다
        private void OnValidate()
        {
            lookup = null;
        }
    }
}
