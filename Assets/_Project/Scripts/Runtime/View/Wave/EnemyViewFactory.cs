using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.View
{
    public class EnemyViewFactory : MonoBehaviour, IEnemyViewFactory, IEnemySpeedProvider
    {
        // 인스펙터에서 EnemyType 별 프리팹, 속도 매핑을 채움
        [SerializeField]
        private List<EnemyPrefabEntry> _enemyPrefabEntries;

        private EnemyPrefabTable _table;

        private void Awake()
        {
            _table = new EnemyPrefabTable(_enemyPrefabEntries);
        }

        public void Create(Enemy enemy)
        {
            var prefab = _table.GetPrefab(enemy.Type);
            var view = Object.Instantiate(prefab);
            view.Bind(enemy);
        }

        public float GetSpeed(EnemyType type)
        {
            return _table.GetSpeed(type);
        }
    }
}
