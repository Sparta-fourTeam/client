using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.View
{
    /// <summary>스킬 IconKey로 스프라이트를 찾는 표. 키에 맞는 항목이 없으면 기본 아이콘을 돌려준다</summary>
    [CreateAssetMenu(fileName = "SkillIconTable", menuName = "Project Nova/Skill Icon Table")]
    public sealed class SkillIconTable : ScriptableObject
    {
        [Serializable]
        private sealed class Entry
        {
            public string Key;
            public Sprite Sprite;
        }

        [SerializeField] private List<Entry> _entries = new List<Entry>();
        [SerializeField] private Sprite _fallback;

        private Dictionary<string, Sprite> _lookup;

        public Sprite Find(string iconKey)
        {
            if (_lookup == null)
            {
                _lookup = new Dictionary<string, Sprite>(_entries.Count);
                foreach (Entry entry in _entries)
                {
                    if (!string.IsNullOrEmpty(entry.Key) && entry.Sprite != null)
                    {
                        _lookup[entry.Key] = entry.Sprite;
                    }
                }
            }

            return !string.IsNullOrEmpty(iconKey) && _lookup.TryGetValue(iconKey, out Sprite sprite) ? sprite : _fallback;
        }

        // 인스펙터에서 항목을 고치면 다음 조회에서 다시 만든다
        private void OnValidate()
        {
            _lookup = null;
        }
    }
}
