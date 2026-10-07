using Game.Core.Messages;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace Game.Core
{
    public class EnemyFactory : MonoBehaviour, IEnemyFactory
    {
        // 몬스터 Id로 프리팹을 찾는 표. 항목별 설정은 이 표에 있고 팩토리에는 표 하나만 연결한다
        [SerializeField]
        private MonsterAssetTable _assets;

        private IPublisher<EnemyHpChanged> _hpChangedPublisher;
        private IPublisher<EnemyDied> _diedPublisher;
        private ISubscriber<EnemyHpChanged> _hpChangedSubscriber;
        private ISubscriber<EnemyDied> _diedSubscriber;
        private GameDataStore _data;

        private int _nextEnemyId;

        [Inject]
        public void Construct(
            IPublisher<EnemyHpChanged> hpChangedPublisher,
            IPublisher<EnemyDied> diedPublisher,
            ISubscriber<EnemyHpChanged> hpchangedSubscriber,
            ISubscriber<EnemyDied> diedSubscriber,
            GameDataStore data
            )
        {
            _hpChangedPublisher = hpChangedPublisher;
            _diedPublisher = diedPublisher;
            _hpChangedSubscriber = hpchangedSubscriber;
            _diedSubscriber = diedSubscriber;
            _data = data;

            ValidateAssets();
        }

        public Enemy Create(int monsterId, Vector2 spawnPosition, bool isSummoned = false)
        {
            var monster = _data.Monsters.GetOrThrow(monsterId);
            var assets = _assets.GetOrThrow(monsterId);

            // 뷰가 위치를 가지므로 먼저 만든다
            var view = Object.Instantiate(assets.prefab, spawnPosition, Quaternion.identity);
            var model = new EnemyModel(
                ++_nextEnemyId, monster.Speed, monster.GetEnemyType(), monster.Hp,
                new EnemyAttackStats(monster.GetAttackType(), monster.Damage, monster.AttackInterval, monster.AttackRange, monster.ProjectileSpeed,
                    monster.BurstCount, monster.BurstInterval),
                _hpChangedPublisher, _diedPublisher,
                PassiveBuilder.BuildPassives(monster), isSummoned, PassiveBuilder.BuildImmunities(monster),
                DamageProfile.From(monster), PassiveBuilder.BuildModifiers(monster));

            view.Bind(model, assets.projectilePrefab, _hpChangedSubscriber, _diedSubscriber);

            return view;
        }

        // 표의 항목이 가리키는 몬스터 행과 프리팹이 제대로 있는지 검사
        private void ValidateAssets()
        {
            if (_assets == null)
            {
                Debug.LogError("[EnemyFactory] MonsterAssetTable이 연결되지 않았습니다.", this);
                return;
            }

            foreach (var pair in _assets.Entries)
            {
                int monsterId = pair.Key;
                var entry = pair.Value;
                string name = $"MonsterId {monsterId}";

                if (!_data.Monsters.Contains(monsterId))
                {
                    Debug.LogError($"[EnemyFactory] {name}: Monsters 테이블에 없는 몬스터입니다.", this);
                    continue;
                }

                if (entry.prefab == null)
                {
                    Debug.LogError($"[EnemyFactory] {name}: prefab이 없습니다.", this);
                }

                if (_data.Monsters.GetOrThrow(monsterId).GetAttackType() == AttackType.Ranged && entry.projectilePrefab == null)
                {
                    Debug.LogError($"[EnemyFactory] {name}: 원거리인데 projectilePrefab이 없습니다.", this);
                }
            }
        }
    }
}
