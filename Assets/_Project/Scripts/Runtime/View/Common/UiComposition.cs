using System;
using UnityEngine;

namespace Game.View
{
    [CreateAssetMenu(menuName = "Project Nova/UI Composition")]
    public sealed class UiComposition : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string Id;
            public GameObject Prefab;
            public string ParentPath;
            public int SiblingIndex;
        }

        public Entry[] Entries = Array.Empty<Entry>();

        public void Validate()
        {
            var ids = new System.Collections.Generic.HashSet<string>();
            foreach (var entry in Entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Id) || !ids.Add(entry.Id))
                {
                    throw new InvalidOperationException($"{name}: missing or duplicate UI id.");
                }

                if (entry.Prefab == null)
                {
                    throw new InvalidOperationException($"{name}/{entry.Id}: missing UI prefab.");
                }
            }
        }
    }
}
