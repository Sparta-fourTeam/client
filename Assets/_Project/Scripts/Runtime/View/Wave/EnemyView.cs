using System;
using System.Collections.Generic;
using Game.Core.Wave;
using UnityEngine;

namespace Game.View.Wave
{
    public class EnemyPrefabTable
    {
        private readonly List<EnemyPrefabEntry> _entries;

        public EnemyPrefabTable(List<EnemyPrefabEntry> entries)
        {
            _entries = entries;
        }

        public EnemyView GetPrefab(EnemyType type)
        {
            return _entries.Find(e => e.Type == type).Prefab;
        }
    }

    [Serializable]
    public struct EnemyPrefabEntry
    {
        public EnemyType Type;
        public EnemyView Prefab;
    }

    public class EnemyView : MonoBehaviour
    {
        private Enemy _enemy;

        public void Bind(Enemy enemy)
        {
            _enemy = enemy;
        }

        private void Update()
        {
            // Instantiate 직후 Bind가 호출 되기 전 null 체크
            if (_enemy == null)
            {
                return;
            }

            transform.position = _enemy.Position;
        }
    }
}
