using System.Collections.Generic;
using Game.Core.Messages;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace Game.Core
{
    public class SkillController : MonoBehaviour, IUpgradeState, IUpgradeChoiceSource
    {
        [SerializeField] private SkillAssetTable assets;
        private Dictionary<int, SkillData> dataTable;
        private ISkillDataProvider dataProvider;
        private IWeaponProgression progression;
        private IStartingSkills startingSkills;
        private Defense.Wall wall;
        private readonly Dictionary<int, int> permanentLevels = new();
        private List<SkillBase> skills = new();
        private IEnemyTargetProvider targetProvider;
        private IBufferedPublisher<SkillChanged> skillChanged;
        private ChildSkillCaster childCaster;
        private ISkillUnlock skillUnlock;
        private IPermanentSkillEffects permanentEffects;
        private Animator attackAnimator;
        private float lastAttackPose = float.NegativeInfinity;
        private static readonly int AttackHash = Animator.StringToHash("Attack");

        [Inject]
        public void Construct(IEnemyTargetProvider targetProvider, IBufferedPublisher<SkillChanged> skillChanged, ISkillDataProvider dataProvider, IWeaponProgression progression = null, Game.Core.Defense.Wall wall = null, IStartingSkills startingSkills = null, ISkillUnlock skillUnlock = null, IPermanentSkillEffects permanentEffects = null)
        {
            this.startingSkills = startingSkills ?? new DefaultStartingSkills();
            this.targetProvider = targetProvider;
            this.skillChanged = skillChanged;
            this.dataProvider = dataProvider;
            this.progression = progression;
            this.wall = wall;
            this.skillUnlock = skillUnlock;
            this.permanentEffects = permanentEffects;
        }

        // 스킬의 기본 설정에 영구 강화 효과를 적용한다. 판 안의 카드 강화는 이 설정 위에 쌓인다 (영구 강화가 없으면 기본 설정 그대로)
        private SkillConfig CreateConfig(SkillData data) =>
            PermanentSkillEffect.Apply(data, permanentEffects?.For(data.progressionId));

        private void Start()
        {
            foreach (var animator in GetComponentsInChildren<Animator>(true))
            {
                foreach (var parameter in animator.parameters)
                {
                    if (parameter.nameHash == AttackHash && parameter.type == AnimatorControllerParameterType.Trigger)
                    { attackAnimator = animator; break; }
                }
                if (attackAnimator != null) { break; }
            }
            dataTable = new Dictionary<int, SkillData>();
            foreach (var data in dataProvider.LoadAll())
            {
                dataTable.Add(data.id, data);
                permanentLevels[data.id] = progression?.GetLevel(data.progressionId) ?? 0;
            }
            childCaster = new ChildSkillCaster(CreateChild,
                id => dataTable.TryGetValue(id, out var child) ? MirroredEffects.For(child, dataTable, this) : null);
            foreach (int id in startingSkills.GetSkillIds()) { AddWeapon(id); }
        }

        // 자식 스킬은 보유 목록에 넣지 않고(틱하지 않는다) 효과가 요청할 때만 시전한다.
        private SkillCaster CreateChild(int skillId)
        {
            GameObject prefab = null;
            if (!dataTable.TryGetValue(skillId, out var data) || (prefab = assets.GetPrefab(data.assetKey)) == null)
            {
                Debug.LogWarning($"[SkillController] 자식 스킬 skillId={skillId}의 데이터 또는 프리팹이 없어 시전하지 못합니다 (SkillAssetTable의 assetKey 확인).");
                return null;
            }
            var child = SkillFactory.Create(data, prefab, transform, targetProvider, wall, CreateConfig(data));
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

            foreach (var owned in skills)
            {
                if (owned.Data.id == weaponId)
                {
                    Debug.LogWarning($"[WeaponController] weaponId={weaponId}는 이미 보유 중입니다.");
                    return false;
                }
            }

            var prefab = assets.GetPrefab(data.assetKey);
            if (prefab == null)
            {
                Debug.LogWarning($"[SkillController] weaponId={weaponId}(assetKey={data.assetKey})에 해당하는 프리팹이 SkillAssetTable에 없습니다.");
                return false;
            }

            var weapon = SkillFactory.Create(data, prefab, transform, targetProvider, wall, CreateConfig(data));
            Debug.Log($"[영구강화] {data.progressionId} Lv.{progression?.GetLevel(data.progressionId) ?? 0} → 피해 {weapon.Config.Stats.Cast.Damage} (기본 {data.baseStats.cast.baseDamage})");
            weapon.UseChildCaster(childCaster);
            weapon.Fired += OnSkillFired;
            skills.Add(weapon);
            PublishSkills();
            return true;
        }

        /// <summary>보유 스킬의 현재 상태(레벨 등)를 HUD에 다시 알린다. 카드를 선택 흐름 밖에서 직접 적용하는 도구(샌드박스)가 적용 뒤에 쓴다</summary>
        public void PublishSkillStatus() => PublishSkills();

        /// <summary>보유 스킬 (조회용)</summary>
        public IReadOnlyList<SkillBase> Skills => skills;

        /// <summary>카탈로그를 읽어 스킬을 얻을 수 있는 상태인지 (Start 이후)</summary>
        public bool IsReady => dataTable != null;

        /// <summary>이 스킬을 얻을 때 쓸 프리팹이 연결돼 있는지</summary>
        public bool HasPrefab(int weaponId) =>
            assets != null && dataTable != null && dataTable.TryGetValue(weaponId, out var data) && assets.HasPrefab(data.assetKey);

        /// <summary>보유 스킬을 모두 정리하고 목록을 비운다 (샌드박스 같은 도구가 처음부터 다시 쌓을 때 쓴다)</summary>
        public void ClearWeapons()
        {
            foreach (var weapon in skills) { weapon.Dispose(); }
            skills.Clear();
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
            skillChanged.Publish(new SkillChanged(new List<ISkillStatus>(skills)));
        }

        private SkillUpgradeChoices Choices => new SkillUpgradeChoices(skills, dataTable.Values, this,
            HasPrefab, skillUnlock == null ? null : skillUnlock.IsUnlocked);

        public List<UpgradeChoice> GetRandomUpgradeChoices(int count) =>
            Choices.Select(count, upperBound => Random.Range(0, upperBound));

        public List<UpgradeChoice> GetUpgradeCandidates() => Choices.Candidates();

        public bool ApplyUpgradeChoice(UpgradeChoice choice)
        {
            if (choice.IsNewWeapon)
            {
                // Use the catalog entry, never caller-supplied weapon data.
                return choice.newSkillData != null && AddWeapon(choice.newSkillData.id);
            }

            if (!Choices.TryApply(choice)) { return false; }
            PublishSkills();
            return true;
        }

        public int GetWeaponLevel(int weaponId)
        {
            var weapon = skills.Find(w => w.Data.id == weaponId);
            return weapon == null ? 0 : weapon.Level;
        }

        public int GetPermanentWeaponLevel(int weaponId)
        {
            return permanentLevels.TryGetValue(weaponId, out var level) ? level : 0;
        }

        public int GetAcquiredCount(int weaponId, string cardId)
        {
            var weapon = skills.Find(w => w.Data.id == weaponId);
            return weapon == null ? 0 : weapon.GetAcquiredCount(cardId);
        }


        private void OnDestroy()
        {
            foreach (var weapon in skills) { weapon.Dispose(); }
            childCaster?.Dispose();
        }

        private void Update()
        {
            foreach (var weapon in skills)
            {
                weapon.Tick();
            }
        }

        private void OnSkillFired()
        {
            if (attackAnimator == null || Time.time - lastAttackPose < 0.16f) { return; }
            lastAttackPose = Time.time;
            attackAnimator.SetTrigger(AttackHash);
        }
    }
}
