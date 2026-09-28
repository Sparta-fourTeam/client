using System.Collections.Generic;
using Game.Core.Wave;
using UnityEngine;

namespace Game.View.Wave
{
    // Wall처럼 씬에 컴포넌트로 올려서 RegisterComponentInHierarchy로 등록
    public class EnemyViewFactory : MonoBehaviour, IEnemyViewFactory
    {
        // 인스펙터에서 EnemyType 별 프리팹 매핑을 채움
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
    }
}
