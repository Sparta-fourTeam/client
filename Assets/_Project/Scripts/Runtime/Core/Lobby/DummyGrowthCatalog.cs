using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>IGrowthCatalog의 하드코딩 구현. 화면 확인용 임시 값이다.
    /// TODO(data): 스킬은 Upgrades.json + PlayerProfile.UpgradeLevel, 캐릭터는 데이터와 API가 생기면 실제 구현으로 교체 (장비는 이미 Upgrades 테이블에서 만든다)</summary>
    public sealed class DummyGrowthCatalog : IGrowthCatalog, IDisposable
    {
        private const int SkillMaxLevel = 30;

        // 장비 칸 순서(오른쪽 2열 6칸 → 왼쪽 1칸)와 표시 이름. 이름은 클라이언트 표시용이다
        private static readonly (string upgradeId, string name)[] EquipSlots =
        {
            ("equipment.hat", "모자"), ("equipment.top", "상의"), ("equipment.shoes", "신발"), ("equipment.weapon", "무기"),
            ("equipment.ring", "반지"), ("equipment.tie", "넥타이"), ("equipment.employee_id", "사원증"),
        };

        private readonly PlayerProfile _profile;
        private readonly GameDataStore _data;

        public CharacterInfo Character { get; } = new CharacterInfo
        {
            Name = "캐릭터",
            Rank = "신입 직원",
            Power = 120,
        };

        /// <summary>장비는 Upgrades 테이블과 PlayerProfile에서 만든다. 강화나 레벨 변화가 바로 보이도록 읽을 때마다 새로 만든다</summary>
        public IReadOnlyList<EquipInfo> Equips => CreateEquips();
        public IReadOnlyList<SkillInfo> Skills { get; }

        public int SkillBooks => 2;
        public int Gems => 550;
        public int Tickets => 0;

        /// <summary>해금 판정은 PlayerProfile의 플레이어 레벨을 따른다. 프로필이 갱신되면(전투 보상 EXP로 레벨이 오른 뒤 등) 해금 여부를 다시 계산한다</summary>
        public DummyGrowthCatalog(PlayerProfile profile, GameDataStore data)
        {
            _profile = profile;
            _data = data;
            Skills = CreateSkills();
            RefreshUnlocks();
            _profile.Changed += OnProfileChanged;
        }

        public void Dispose()
        {
            _profile.Changed -= OnProfileChanged;
        }

        private void OnProfileChanged(PlayerProfile _) => RefreshUnlocks();

        // 스킬 칸의 해금은 플레이어 레벨로 정한다(캐릭터에는 레벨이 없다). 장비 칸은 Equips를 읽을 때마다 계산한다
        private void RefreshUnlocks()
        {
            int level = _profile.Level;
            foreach (var skill in Skills)
            {
                skill.IsUnlocked = skill.UnlockLevel <= level;
            }
        }

        private List<EquipInfo> CreateEquips()
        {
            var list = new List<EquipInfo>();
            for (int i = 0; i < EquipSlots.Length; i++)
            {
                var (upgradeId, name) = EquipSlots[i];
                var def = _data.Upgrades.GetOrThrow(upgradeId);
                int level = _profile.UpgradeLevel(upgradeId);
                bool isMax = level >= def.MaxLevel;
                int next = level + 1;

                var effects = new List<EquipEffect>();
                foreach (var effect in def.Effects)
                {
                    effects.Add(new EquipEffect(EquipEffectText.Format(effect, level, isMax)));
                }

                list.Add(new EquipInfo
                {
                    Slot = i,
                    UpgradeId = upgradeId,
                    Name = name,
                    Level = level,
                    MaxLevel = def.MaxLevel,
                    UnlockLevel = def.UnlockLevel,
                    IsUnlocked = _profile.Level >= def.UnlockLevel,
                    Stats = new[]
                    {
                        new StatLine("장비 레벨", $"{level} / {def.MaxLevel}"),
                        new StatLine("해금 레벨", def.UnlockLevel.ToString()),
                    },
                    Effects = effects,
                    CoinCost = isMax ? 0 : def.CostAt(next),
                    MaterialItemId = def.MaterialItemId,
                    MaterialCost = isMax || !def.UsesMaterial ? 0 : def.MaterialAt(next),
                });
            }

            return list;
        }

        // 6개는 열려 있고, 나머지는 플레이어 레벨 4부터 하나씩 열린다
        private List<SkillInfo> CreateSkills()
        {
            (string id, string name, string desc, int level, int attack, float cooldown)[] open =
            {
                ("shuriken", "단검", "가장 가까운 적에게 단검을 던진다", 3, 10, 1.0f),
                ("fireball", "화염탄", "적에게 닿으면 폭발하는 화염탄을 쏜다", 0, 15, 2.0f),
                ("frost", "빙결", "주변 적을 얼려 느리게 만든다", 0, 8, 3.0f),
                ("lightning", "낙뢰", "무작위 적에게 벼락을 떨어뜨린다", 0, 20, 2.5f),
                ("thorn", "가시", "바닥에 가시를 깔아 지나가는 적을 찌른다", 0, 6, 4.0f),
                ("flash", "섬광", "앞쪽 적을 꿰뚫는 빛줄기를 쏜다", 0, 12, 3.0f),
            };

            var list = new List<SkillInfo>();
            foreach (var n in open)
            {
                int attack = Attack(n.attack, n.level);
                float cooldown = Cooldown(n.cooldown, n.level);
                list.Add(new SkillInfo
                {
                    Id = n.id,
                    Name = n.name,
                    Description = n.desc,
                    Level = n.level,
                    MaxLevel = SkillMaxLevel,
                    UnlockLevel = 1,
                    Stats = new[]
                    {
                        new StatLine("공격력", attack.ToString(), increase: $"+{Attack(n.attack, n.level + 1) - attack}"),
                        new StatLine("쿨타임", $"{cooldown:0.##}초", increase: $"{Cooldown(n.cooldown, n.level + 1) - cooldown:0.##}초"),
                    },
                    CoinCost = 100 + n.level * 50,
                    BookCost = 1 + n.level / 3,
                    LevelRewards = CreateRewards(n.name),
                });
            }

            for (int unlockLevel = 4; unlockLevel <= 15; unlockLevel++)
            {
                list.Add(new SkillInfo
                {
                    Id = "locked" + unlockLevel,
                    Name = "???",
                    Description = string.Empty,
                    MaxLevel = SkillMaxLevel,
                    UnlockLevel = unlockLevel,
                    Stats = new StatLine[0],
                    LevelRewards = new LevelReward[0],
                });
            }
            return list;
        }

        // 임시: 데이터 시트의 영구 레벨 구간(5/9/13/17/21)은 카드 해금·변형, 나머지는 공격력 증가로 채운다
        // TODO(data): #142 계약이 확정되면 Upgrades 데이터에서 읽는다
        private static List<LevelReward> CreateRewards(string skillName)
        {
            var list = new List<LevelReward>();
            for (int level = 1; level <= SkillMaxLevel; level++)
            {
                string text = level switch
                {
                    1 => $"[{skillName} 강화] 선택지 잠금해제",
                    5 or 13 or 21 => "새 강화 선택지 잠금해제",
                    9 or 17 => "강화 선택지가 (+)로 바뀝니다",
                    _ => $"{skillName} 공격력 +8%",
                };
                list.Add(new LevelReward { Level = level, Text = text });
            }

            return list;
        }

        // 임시 공식: 공격력은 레벨당 기본값의 20%씩, 쿨타임은 3레벨마다 5%씩 줄어든다
        private static int Attack(int baseAttack, int level) => baseAttack + baseAttack * level / 5;
        private static float Cooldown(float baseCooldown, int level) => baseCooldown * (1f - 0.05f * level / 3f);
    }
}
