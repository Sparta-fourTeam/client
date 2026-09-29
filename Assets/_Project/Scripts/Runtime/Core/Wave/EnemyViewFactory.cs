using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    public class EnemyViewFactory : MonoBehaviour, IEnemyViewFactory
    {
        // 인스펙터에서 EnemyType 별 프리팹, 속도 매핑을 채움
        [SerializeField]
        private List<EnemyPrefabEntry> _enemyPrefabEntries;

        private EnemyPrefabTable _table;

        private void Awake()
        {
            _table = new EnemyPrefabTable(_enemyPrefabEntries);
        }

        public Enemy Create(Vector2 spawnPosition, EnemyType type)
        {
            var entry = _table.GetEntry(type);
            var view = Object.Instantiate(entry.Prefab);
            var enemy = new Enemy(spawnPosition, entry.Speed, type);
            view.Bind(enemy);

            return enemy;
        }
    }
}
