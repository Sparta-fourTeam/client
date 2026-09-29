using System;
using System.Collections.Generic;

namespace Game.Core
{
    public class EnemyPrefabTable
    {
        private readonly List<EnemyPrefabEntry> _entries;

        public EnemyPrefabTable(List<EnemyPrefabEntry> entries)
        {
            _entries = entries;
        }

        public EnemyPrefabEntry GetEntry(EnemyType type)
        {
            var exists = _entries.Find(e => e.Type == type);
            return exists.Prefab == null ? throw new InvalidOperationException($"EnemyType.{type}에 해당하는 EnemyPrefabEntry가 없습니다.") : exists;
        }
    }
}
