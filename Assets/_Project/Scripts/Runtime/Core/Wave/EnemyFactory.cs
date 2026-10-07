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
        private IRandomProvider _randomProvider;
        private GameDataStore _data;

        private int _nextEnemyId;

        private void Awake()
        {
            _table = new EnemyPrefabTable(_enemyPrefabEntries);

            foreach (var entry in _enemyPrefabEntries)
            {
                ValidateEntry(entry);
            }

            // EnemyType마다 엔트리 최소 1개 있는지 체크
            foreach (EnemyType type in System.Enum.GetValues(typeof(EnemyType)))
            {
                if (!_enemyPrefabEntries.Exists(e => e.Type == type && !e.SpawnOnly))
                {
                    Debug.LogError($"[EnemyFactory] EnemyType.{type} 엔트리가 없습니다.", this);
                }
            }
        }

        [Inject]
        public void Construct(
            IPublisher<EnemyHpChanged> hpChangedPublisher,
            IPublisher<EnemyDied> diedPublisher,
            ISubscriber<EnemyHpChanged> hpchangedSubscriber,
            ISubscriber<EnemyDied> diedSubscriber,
            IRandomProvider randomProvider,
            GameDataStore data
            )
        {
            _hpChangedPublisher = hpChangedPublisher;
            _diedPublisher = diedPublisher;
            _hpChangedSubscriber = hpchangedSubscriber;
            _diedSubscriber = diedSubscriber;
            _randomProvider = randomProvider;
            _data = data;

            // 프리팹 엔트리가 가리키는 몬스터 행이 모두 있어야 한다
            foreach (var entry in _enemyPrefabEntries)
            {
                if (!_data.Monsters.Contains(entry.MonsterId))
                {
                    Debug.LogError($"[EnemyFactory] EnemyType.{entry.Type}: Monsters 테이블에 MonsterId {entry.MonsterId}가 없습니다.", this);
                }
                else if (entry.AttackType == AttackType.Ranged && _data.Monsters.GetOrThrow(entry.MonsterId).ProjectileSpeed <= 0f)
                {
                    Debug.LogError($"[EnemyFactory] EnemyType.{entry.Type}: 원거리인데 MonsterId {entry.MonsterId}의 ProjectileSpeed가 0입니다.", this);
                }
            }
        }

        public Enemy Create(Vector2 spawnPosition, EnemyType type)
        {
            var entry = _table.GetRandomEntry(type, _randomProvider);
            return Build(entry, spawnPosition, isSummoned: false);
        }

        public Enemy CreateByMonsterId(int monsterId, Vector2 spawnPosition)
        {
            var entry = _table.GetEntryByMonsterId(monsterId);
            return Build(entry, spawnPosition, isSummoned: true);
        }

        private Enemy Build(EnemyPrefabEntry entry, Vector2 spawnPosition, bool isSummoned)
        {
            var monster = _data.Monsters.GetOrThrow(entry.MonsterId);
            var model = new EnemyModel(
                ++_nextEnemyId, monster.Speed, entry.Type, monster.Hp,
                entry.CreateAttackStats(monster),
                _hpChangedPublisher, _diedPublisher,
                PassiveBuilder.BuildPassives(monster), isSummoned, PassiveBuilder.BuildImmunities(monster));

            var view = Object.Instantiate(entry.Prefab, spawnPosition, Quaternion.identity);
            view.Bind(model, entry.ProjectilePrefab, _hpChangedSubscriber, _diedSubscriber);

            return view;
        }

        // 인스펙터 엔트리 검사
        private void ValidateEntry(EnemyPrefabEntry entry)
        {
            string name = $"EnemyType.{entry.Type}";

            if (entry.Prefab == null)
            {
                Debug.LogError($"[EnemyFactory] {name}: Prefab이 없습니다.", this);
            }

            if (entry.AttackType == AttackType.Ranged && entry.ProjectilePrefab == null)
            {
                Debug.LogError($"[EnemyFactory] {name}: 원거리인데 ProjectilePrefab이 없습니다.", this);
            }
        }
    }
}
