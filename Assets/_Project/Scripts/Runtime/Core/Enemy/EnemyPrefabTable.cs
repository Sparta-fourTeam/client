using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    public class EnemyPrefabTable
    {
        private readonly Dictionary<EnemyType, List<EnemyPrefabEntry>> _byType = new();

        public EnemyPrefabTable(List<EnemyPrefabEntry> entries)
        {
            foreach(var e in entries)
            {
                if (!_byType.TryGetValue(e.Type, out var list))
                    _byType[e.Type] = list = new List<EnemyPrefabEntry>();

                list.Add(e);
            }
        }

        public EnemyPrefabEntry GetRandomEntry(EnemyType type, IRandomProvider random)
        {
            if (!_byType.TryGetValue(type, out var list) || list.Count == 0)
                throw new InvalidOperationException($"EnemyType.{type}에 해당하는 EnemyPrefabEntry가 없습니다.");

            int index = Mathf.Clamp((int)random.Range(0f, list.Count), 0, list.Count - 1);
            return list[index];
        }
    }
}
