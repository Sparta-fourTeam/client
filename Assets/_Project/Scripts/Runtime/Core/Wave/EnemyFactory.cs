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

            foreach (var entry in _enemyPrefabEntries)
            {
                ValidateEntry(entry);
            }
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
                entry.CreateAttackStats(),
                _hpChangedPublisher, _diedPublisher);

            var view = Object.Instantiate(entry.Prefab, spawnPosition, Quaternion.identity);
            view.Bind(enemy, entry.ProjectilePrefab,_hpChangedSubscriber, _diedSubscriber);

            return enemy;
        }

        // 인스펙터 엔트리 검사
        private void ValidateEntry(EnemyPrefabEntry entry)
        {
            string name = $"EnemyType.{entry.Type}";

            if (entry.Prefab == null)
                Debug.LogError($"[EnemyFactory] {name}: Prefab이 없습니다.", this);
            if (entry.MaxHp <= 0)
                Debug.LogError($"[EnemyFactory] {name}: MaxHp는 1 이상이어야 합니다.", this);
            if (entry.AttackInterval <= 0f)
                Debug.LogError($"[EnemyFactory] {name}: AttackInterval은 0보다 커야 합니다.", this);
            if (entry.AttackRange < 0f)
                Debug.LogError($"[EnemyFactory] {name}: AttackRange는 음수일 수 없습니다.", this);
            if (entry.AttackType == AttackType.Ranged && entry.ProjectilePrefab == null)
                Debug.LogError($"[EnemyFactory] {name}: 원거리인데 ProjectilePrefab이 없습니다.", this);
        }
    }
}
