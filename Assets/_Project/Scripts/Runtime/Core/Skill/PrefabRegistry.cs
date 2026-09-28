using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    [CreateAssetMenu(fileName = "PrefabRegistry", menuName = "Game/Prefab Registry")]
    public class PrefabRegistry : ScriptableObject
    {
        [System.Serializable]
        public class Entry { public string key; public GameObject prefab; }

        [SerializeField] private List<Entry> entries;
        private Dictionary<string, GameObject> lookup;

        public GameObject GetPrefab(string key)
        {
            if (lookup == null)
            {
                BuildLookup();
            }
            return lookup.TryGetValue(key, out var prefab) ? prefab : null;
        }

        private void BuildLookup()
        {
            lookup = new Dictionary<string, GameObject>();
            foreach (var entry in entries)
            {
                lookup[entry.key] = entry.prefab;
            }
        }
    }
}
