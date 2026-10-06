using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    public class EnemyPrefabTable
    {
        // 웨이브 스폰이 무작위로 고르는 엔트리. SpawnOnly는 들어가지 않는다
        private readonly Dictionary<EnemyType, List<EnemyPrefabEntry>> _byType = new();
        private readonly Dictionary<int, EnemyPrefabEntry> _byMonsterId = new();

        public EnemyPrefabTable(List<EnemyPrefabEntry> entries)
        {
            foreach (var e in entries)
            {
                _byMonsterId.TryAdd(e.MonsterId, e);

                if (e.SpawnOnly)
                {
                    continue;
                }

                if (!_byType.TryGetValue(e.Type, out var list))
                {
                    _byType[e.Type] = list = new List<EnemyPrefabEntry>();
                }

                list.Add(e);
            }
        }

        public EnemyPrefabEntry GetRandomEntry(EnemyType type, IRandomProvider random)
        {
            if (!_byType.TryGetValue(type, out var list) || list.Count == 0)
            {
                throw new InvalidOperationException($"EnemyType.{type}에 해당하는 EnemyPrefabEntry가 없습니다.");
            }

            int index = Mathf.Clamp((int)random.Range(0f, list.Count), 0, list.Count - 1);
            return list[index];
        }

        // 분열·소환처럼 몬스터를 직접 지정할 때 쓴다 (SpawnOnly 엔트리 포함)
        public EnemyPrefabEntry GetEntryByMonsterId(int monsterId)
        {
            if (!_byMonsterId.TryGetValue(monsterId, out var entry))
            {
                throw new InvalidOperationException($"MonsterId {monsterId}에 해당하는 EnemyPrefabEntry가 없습니다.");
            }

            return entry;
        }
    }
}
