using System.Collections.Generic;
using Game.Core.Messages;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace Game.Core
{
    [System.Serializable]
    public class WeaponPrefabEntry
    {
        public int id;
        public GameObject prefab;
    }


    public class WeaponController : MonoBehaviour, IUpgradeState
    {
        [SerializeField] private List<WeaponPrefabEntry> prefabEntries;
        private Dictionary<int, WeaponData> dataTable;
        private IWeaponDataProvider dataProvider;
        private IWeaponProgression progression;
        private Game.Core.Defense.Wall wall;
        private readonly Dictionary<int, int> permanentLevels = new();
        private List<WeaponBase> weapons = new List<WeaponBase>();
        private IEnemyTargetProvider targetProvider;
        private IBufferedPublisher<SkillChanged> skillChanged;

        [Inject]
        public void Construct(IEnemyTargetProvider targetProvider, IBufferedPublisher<SkillChanged> skillChanged, IWeaponDataProvider dataProvider, IWeaponProgression progression = null, Game.Core.Defense.Wall wall = null)
        {
            this.targetProvider = targetProvider;
            this.skillChanged = skillChanged;
            this.dataProvider = dataProvider;
            this.progression = progression;
            this.wall = wall;
        }

        private void Start()
        {
            dataTable = new Dictionary<int, WeaponData>();
            foreach (var data in dataProvider.LoadAll())
            {
                dataTable.Add(data.id, data);
                permanentLevels[data.id] = progression?.GetLevel(data.progressionId) ?? 0;
            }
            AddWeapon(3);
        }


        public bool AddWeapon(int weaponId)
        {
            if (!dataTable.TryGetValue(weaponId, out var data))
            {
                Debug.LogWarning($"[WeaponController] weaponId={weaponId}에 해당하는 WeaponData가 없습니다.");
                return false;
            }

            foreach (var owned in weapons)
            {
                if (owned.Data.id == weaponId)
                {
                    Debug.LogWarning($"[WeaponController] weaponId={weaponId}는 이미 보유 중입니다.");
                    return false;
                }
            }

            var entry = prefabEntries.Find(e => e.id == weaponId);
            if (entry == null || entry.prefab == null)
            {
                Debug.LogWarning($"[WeaponController] weaponId={weaponId}에 해당하는 프리팹 엔트리가 없습니다.");
                return false;
            }

            weapons.Add(WeaponFactory.Create(data, entry.prefab, transform, targetProvider, wall));
            PublishSkills();
            return true;
        }

        /// <summary>보유 무기 전체의 스냅샷을 HUD에 알린다. Buffered로 발행해야 나중에 켜진 구독자도 현재 목록을 받는다</summary>
        private void PublishSkills()
        {
            skillChanged.Publish(new SkillChanged(new List<ISkillStatus>(weapons)));
        }

        private WeaponUpgradeChoices Choices => new WeaponUpgradeChoices(weapons, dataTable.Values, this,
            id => prefabEntries.Exists(e => e.id == id && e.prefab != null));

        public List<UpgradeChoice> GetRandomUpgradeChoices(int count) =>
            Choices.Select(count, upperBound => Random.Range(0, upperBound));

        public bool ApplyUpgradeChoice(UpgradeChoice choice)
        {
            if (choice.IsNewWeapon)
            {
                // Use the catalog entry, never caller-supplied weapon data.
                return choice.NewWeaponData != null && AddWeapon(choice.NewWeaponData.id);
            }

            if (!Choices.TryApply(choice)) { return false; }
            PublishSkills();
            return true;
        }

        public int GetWeaponLevel(int weaponId)
        {
            var weapon = weapons.Find(w => w.Data.id == weaponId);
            return weapon == null ? 0 : weapon.Level;
        }

        public int GetPermanentWeaponLevel(int weaponId)
        {
            return permanentLevels.TryGetValue(weaponId, out var level) ? level : 0;
        }

        public int GetAcquiredCount(int weaponId, string cardId)
        {
            var weapon = weapons.Find(w => w.Data.id == weaponId);
            return weapon == null ? 0 : weapon.GetAcquiredCount(cardId);
        }


        private void OnDestroy()
        {
            foreach (var weapon in weapons) { weapon.Dispose(); }
        }

        private void Update()
        {
            foreach (var weapon in weapons)
            {
                weapon.Tick();
            }
        }
    }
}
