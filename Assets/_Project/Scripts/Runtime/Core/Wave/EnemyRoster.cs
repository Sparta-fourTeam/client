using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>스테이지에 등장하는 몬스터를 등급별로 모아 두고 웨이브 스폰이 무작위로 고른다.
    /// 분열·소환으로만 나오는 몬스터는 스테이지의 등장 목록(MonsterIds)에 없으므로 여기에 들어오지 않는다</summary>
    public sealed class EnemyRoster
    {
        private readonly Dictionary<EnemyType, List<MonsterDefinition>> _byType = new();

        public EnemyRoster(IEnumerable<MonsterDefinition> monsters)
        {
            foreach (var monster in monsters)
            {
                var type = monster.GetEnemyType();
                if (!_byType.TryGetValue(type, out var list))
                {
                    _byType[type] = list = new List<MonsterDefinition>();
                }

                list.Add(monster);
            }
        }

        /// <summary>해당 등급의 몬스터 하나. 그 등급이 스테이지에 없으면(예: 엘리트가 없는 스테이지) 일반 등급에서 고른다</summary>
        public MonsterDefinition Pick(EnemyType type, IRandomProvider random)
        {
            if (!_byType.TryGetValue(type, out var list) || list.Count == 0)
            {
                if (!_byType.TryGetValue(EnemyType.Normal, out list) || list.Count == 0)
                {
                    throw new InvalidOperationException($"스테이지에 EnemyType.{type}도 일반 등급 몬스터도 없습니다.");
                }
            }

            if (list.Count == 1)
            {
                return list[0];
            }

            int index = Mathf.Clamp((int)random.Range(0f, list.Count), 0, list.Count - 1);
            return list[index];
        }
    }
}
