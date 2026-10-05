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
        private IStartingSkills startingSkills;
        private Game.Core.Defense.Wall wall;
        private readonly Dictionary<int, int> permanentLevels = new();
        private List<WeaponBase> weapons = new List<WeaponBase>();
        private IEnemyTargetProvider targetProvider;
        private IBufferedPublisher<SkillChanged> skillChanged;
        private ChildSkillCaster childCaster;

        [Inject]
        public void Construct(IEnemyTargetProvider targetProvider, IBufferedPublisher<SkillChanged> skillChanged, IWeaponDataProvider dataProvider, IWeaponProgression progression = null, Game.Core.Defense.Wall wall = null, IStartingSkills startingSkills = null)
        {
            this.startingSkills = startingSkills ?? new DefaultStartingSkills();
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
            childCaster = new ChildSkillCaster(CreateChild);
            foreach (int id in startingSkills.GetSkillIds()) { AddWeapon(id); }
        }

        // 자식 스킬은 보유 목록에 넣지 않고(틱하지 않는다) 효과가 요청할 때만 시전한다.
        private SkillCaster CreateChild(int skillId)
        {
            var entry = prefabEntries.Find(e => e.id == skillId);
            if (!dataTable.TryGetValue(skillId, out var data) || entry == null || entry.prefab == null)
            {
                Debug.LogWarning($"[WeaponController] 자식 스킬 skillId={skillId}의 데이터 또는 프리팹 엔트리가 없어 시전하지 못합니다.");
                return null;
            }
            var child = WeaponFactory.Create(data, entry.prefab, transform, targetProvider, wall);
            child.UseChildCaster(childCaster);
            return child;
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

            var weapon = WeaponFactory.Create(data, entry.prefab, transform, targetProvider, wall);
            weapon.UseChildCaster(childCaster);
            weapons.Add(weapon);
            PublishSkills();
            return true;
        }

        /// <summary>보유 스킬 (조회용)</summary>
        public IReadOnlyList<WeaponBase> Weapons => weapons;

        /// <summary>카탈로그를 읽어 스킬을 얻을 수 있는 상태인지 (Start 이후)</summary>
        public bool IsReady => dataTable != null;

        /// <summary>이 스킬을 얻을 때 쓸 프리팹이 연결돼 있는지</summary>
        public bool HasPrefab(int weaponId) => prefabEntries != null && prefabEntries.Exists(e => e.id == weaponId && e.prefab != null);

        /// <summary>보유 스킬을 모두 정리하고 목록을 비운다 (샌드박스 같은 도구가 처음부터 다시 쌓을 때 쓴다)</summary>
        public void ClearWeapons()
        {
            foreach (var weapon in weapons) { weapon.Dispose(); }
            weapons.Clear();
            PublishSkills();
        }

        /// <summary>영구 레벨 표를 진행 서비스에서 다시 읽는다 ((+) 변형 같은 영구 레벨 의존 규칙이 바뀐 값을 쓰게 한다)</summary>
        public void RefreshPermanentLevels()
        {
            if (dataTable == null) { return; }
            foreach (var data in dataTable.Values) { permanentLevels[data.id] = progression?.GetLevel(data.progressionId) ?? 0; }
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
            childCaster?.Dispose();
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
