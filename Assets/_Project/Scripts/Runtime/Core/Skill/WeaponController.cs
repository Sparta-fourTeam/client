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


    public class WeaponController : MonoBehaviour
    {
        [SerializeField] private List<WeaponPrefabEntry> prefabEntries;
        private Dictionary<int, WeaponData> testDataTable;
        private List<WeaponBase> weapons = new List<WeaponBase>();
        private IEnemyTargetProvider targetProvider;
        private IBufferedPublisher<SkillChanged> skillChanged;

        [Inject]
        public void Construct(IEnemyTargetProvider targetProvider, IBufferedPublisher<SkillChanged> skillChanged)
        {
            this.targetProvider = targetProvider;
            this.skillChanged = skillChanged;
        }

        private void Start()
        {
            TestTable();
            AddWeapon(3);
        }

        private void TestTable()
        {
            testDataTable = new Dictionary<int, WeaponData>
            {
                {
                    1, new WeaponData
                    {
                        id = 1, name = "화살", iconKey = "weapon_arrow", castType = CastType.Projectile,
                        baseStats = new WeaponBaseStats { cooldown = 1.0f, baseDamage = 10f, range = 22f, speed = 20f, hitCount = 1 },
                        maxLevel = 99,
                        upgrades = new List<WeaponUpgradeOption>
                        {
                            new WeaponUpgradeOption
                            {
                                id = "arrow_multishot", name = "다중 사격(+)", desc = "발사 수 +1 / 공격력 -20%",
                                maxPickCount = 2,
                                effects = new List<StatEffect> { new StatEffect { type = UpgradeType.ProjectileCount, value = 1f }, new StatEffect { type = UpgradeType.Damage, value = -20f } }
                            },
                            new WeaponUpgradeOption
                            {
                                id = "arrow_sharp", name = "날카로운 화살", desc = "공격력 +60%",
                                maxPickCount = 3,
                                effects = new List<StatEffect> { new StatEffect { type = UpgradeType.Damage, value = 60f } }
                            },
                            new WeaponUpgradeOption
                            {
                                id = "arrow_light", name = "경량 화살", desc = "공격력 +25% / 투척 속도 +25%",
                                maxPickCount = 2,
                                effects = new List<StatEffect> { new StatEffect { type = UpgradeType.Damage, value = 25f }, new StatEffect { type = UpgradeType.AttackSpeed, value = 25f } }
                            },
                            new WeaponUpgradeOption
                            {
                                id = "arrow_burst", name = "연속 사격(+)", desc = "시전 수 +1 / 공격력 -30%",
                                maxPickCount = 1, requiredCardIds = new[] { "arrow_sharp" },
                                effects = new List<StatEffect> { new StatEffect { type = UpgradeType.ProjectileCount, value = 1f }, new StatEffect { type = UpgradeType.Damage, value = -30f } }
                            },
                            new WeaponUpgradeOption
                            {
                                id = "arrow_rapid", name = "속사", desc = "쿨타임 -10%",
                                maxPickCount = 1, requiredCardIds = new[] { "arrow_burst" },
                                effects = new List<StatEffect> { new StatEffect { type = UpgradeType.AttackSpeed, value = 10f } }
                            },
                        }
                    }
                },

                {
                    2, new WeaponData
                    {
                        id = 2, name = "화염탄", iconKey = "weapon_fireball", castType = CastType.Projectile,
                        baseStats = new WeaponBaseStats { cooldown = 1.8f, baseDamage = 12f, range = 20f, speed = 16f, hitCount = 1 },
                        maxLevel = 99,
                        upgrades = new List<WeaponUpgradeOption>
                        {
                            new WeaponUpgradeOption
                            {
                                id = "fireball_burst", name = "연속 화염탄", desc = "추가 시전 +1",
                                maxPickCount = 2,
                                effects = new List<StatEffect> { new StatEffect { type = UpgradeType.ProjectileCount, value = 1f } }
                            },
                        }
                    }
                },

                {
                    3, new WeaponData
                    {
                        id = 3, name = "낙뢰", iconKey = "weapon_lightning", castType = CastType.Hitscan,
                        baseStats = new WeaponBaseStats { cooldown = 2.5f, baseDamage = 25f, range = 20f, speed = 14f, hitCount = 1 },
                        maxLevel = 99,
                        upgrades = new List<WeaponUpgradeOption>
                        {
                            new WeaponUpgradeOption
                            {
                                id = "lightning_damage", name = "낙뢰 피해 증폭", desc = "공격력 +80%",
                                maxPickCount = 4,
                                effects = new List<StatEffect> { new StatEffect { type = UpgradeType.Damage, value = 80f } }
                            },
                            new WeaponUpgradeOption
                            {
                                id = "lightning_burst", name = "연속 낙뢰(+)", desc = "시전 수 +1 / 공격력 -20%",
                                maxPickCount = 3,
                                effects = new List<StatEffect> { new StatEffect { type = UpgradeType.ProjectileCount, value = 1f }, new StatEffect { type = UpgradeType.Damage, value = -20f } }
                            },
                        }
                    }
                },
            };
        }


        public void AddWeapon(int weaponId)
        {
            if (!testDataTable.TryGetValue(weaponId, out var data))
            {
                Debug.LogWarning($"[WeaponController] weaponId={weaponId}에 해당하는 WeaponData가 없습니다.");
                return;
            }

            foreach (var owned in weapons)
            {
                if (owned.Data.id == weaponId)
                {
                    Debug.LogWarning($"[WeaponController] weaponId={weaponId}는 이미 보유 중입니다.");
                    return;
                }
            }

            var entry = prefabEntries.Find(e => e.id == weaponId);
            if (entry == null)
            {
                Debug.LogWarning($"[WeaponController] weaponId={weaponId}에 해당하는 프리팹 엔트리가 없습니다.");
                return;
            }

            weapons.Add(WeaponFactory.Create(data, entry.prefab, transform, targetProvider));
            PublishSkills();
        }

        /// <summary>보유 무기 전체의 스냅샷을 HUD에 알린다. Buffered로 발행해야 나중에 켜진 구독자도 현재 목록을 받는다</summary>
        private void PublishSkills()
        {
            skillChanged.Publish(new SkillChanged(new List<ISkillStatus>(weapons)));
        }

        public void WeaponLevelUp(int weaponId)
        {
        }

        public struct UpgradeChoice
        {
            public bool IsNewWeapon;
            public WeaponData NewWeaponData;
            public WeaponBase Weapon;
            public WeaponUpgradeOption Option;
        }

        public List<UpgradeChoice> GetRandomUpgradeChoices(int count)
        {
            var pool = new List<UpgradeChoice>();

            var ownedIds = new HashSet<int>();
            foreach (var weapon in weapons)
            {
                ownedIds.Add(weapon.Data.id);
            }

            foreach (var weapon in weapons)
            {
                if (weapon.IsMaxLevel)
                {
                    continue;
                }

                foreach (var option in weapon.Data.upgrades)
                {
                    if (weapon.GetAcquiredCount(option.id) >= option.maxPickCount)
                    {
                        continue;
                    }

                    if (!RequirementsMet(option, ownedIds))
                    {
                        continue;
                    }

                    if (!CardRequirementsMet(option, weapon))
                    {
                        continue;
                    }

                    pool.Add(new UpgradeChoice { IsNewWeapon = false, Weapon = weapon, Option = option });
                }
            }

            foreach (var kv in testDataTable)
            {
                if (!ownedIds.Contains(kv.Key))
                {
                    pool.Add(new UpgradeChoice { IsNewWeapon = true, NewWeaponData = kv.Value });
                }
            }

            for (int i = pool.Count - 1; i >= 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }

            return pool.GetRange(0, Mathf.Min(count, pool.Count));
        }

        public void ApplyUpgradeChoice(UpgradeChoice choice)
        {
            if (choice.IsNewWeapon)
            {
                AddWeapon(choice.NewWeaponData.id);
            }
            else
            {
                choice.Weapon.LevelUp(choice.Option);
                PublishSkills();
            }
        }

        private bool RequirementsMet(WeaponUpgradeOption option, HashSet<int> ownedIds)
        {
            if (option.requiredWeaponIds == null)
            {
                return true;
            }

            foreach (var requiredId in option.requiredWeaponIds)
            {
                if (!ownedIds.Contains(requiredId))
                {
                    return false;
                }
            }

            return true;
        }

        private bool CardRequirementsMet(WeaponUpgradeOption option, WeaponBase weapon)
        {
            if (option.requiredCardIds == null)
            {
                return true;
            }

            foreach (var requiredCardId in option.requiredCardIds)
            {
                if (weapon.GetAcquiredCount(requiredCardId) < 1)
                {
                    return false;
                }
            }

            return true;
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
