using System.Collections.Generic;
using Game.Core.Messages;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace Game.Core
{
    public class EnemyFactory : MonoBehaviour, IEnemyFactory
    {
        // 인스펙터에서 EnemyType 별 프리팹, 속도 매핑을 채움
        [SerializeField]
        private List<EnemyPrefabEntry> _enemyPrefabEntries;

        private EnemyPrefabTable _table;

        private IPublisher<EnemyHpChanged> _hpChangedPublisher;
        private IPublisher<EnemyDied> _diedPublisher;
        private ISubscriber<EnemyHpChanged> _hpChangedSubscriber;
        private ISubscriber<EnemyDied> _diedSubscriber;

        private int _nextEnemyId;

        private void Awake()
        {
            _table = new EnemyPrefabTable(_enemyPrefabEntries);
        }

        [Inject]
        public void Construct(
            IPublisher<EnemyHpChanged> hpChangedPublisher,
            IPublisher<EnemyDied> diedPublisher,
            ISubscriber<EnemyHpChanged> hpchangedSubscriber,
            ISubscriber<EnemyDied> diedSubscriber
            )
        {
            _hpChangedPublisher = hpChangedPublisher;
            _diedPublisher = diedPublisher;
            _hpChangedSubscriber = hpchangedSubscriber;
            _diedSubscriber = diedSubscriber;
        }

        public EnemyModel Create(Vector2 spawnPosition, EnemyType type)
        {
            var entry = _table.GetEntry(type);
            var enemy = new EnemyModel(
                ++_nextEnemyId, spawnPosition, entry.Speed, type, entry.MaxHp,
                _hpChangedPublisher, _diedPublisher);

            var view = Object.Instantiate(entry.Prefab, spawnPosition, Quaternion.identity);
            view.Bind(enemy, _hpChangedSubscriber, _diedSubscriber);

            return enemy;
        }
    }
}
