using System;
using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.View
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

        // 타입에 해당하는 이동 속도 조회
        public float GetSpeed(EnemyType type)
        {
            return _entries.Find(e => e.Type == type).Speed;
        }
    }

    [Serializable]
    public struct EnemyPrefabEntry
    {
        public EnemyType Type;
        public EnemyView Prefab;
        public float Speed; // 해당 타입의 이동 속도
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
