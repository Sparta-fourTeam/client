using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    /// <summary>IGrowthCatalog의 임시 구현.
    /// 장비는 Upgrades 테이블과 PlayerProfile에서, 스킬 칸은 Skills.json(기획 시트의 스킬 탭)에서 만든다.
    /// Upgrades 테이블에 영구 강화가 정의된 스킬(화살·화염구·벼락)은 레벨·비용도 테이블과 PlayerProfile에서 온다.
    /// TODO(data): 캐릭터 정보와 보석·티켓은 데이터와 API가 생기면 실제 구현으로 교체</summary>
    public sealed class DummyGrowthCatalog : IGrowthCatalog, IDisposable
    {
        // 강화 데이터가 없는 스킬의 표시용 최대 레벨. 강화할 수 없으므로 레벨 0에서 MAX로 보이지 않게만 한다
        private const int NoUpgradeMaxLevel = 1;

        // 장비 칸 순서(오른쪽 2열 6칸 → 왼쪽 1칸)와 표시 이름. 이름은 클라이언트 표시용이다
        private static readonly (string upgradeId, string name)[] EquipSlots =
        {
            ("equipment.hat", "후드"), ("equipment.top", "로브"), ("equipment.shoes", "장화"), ("equipment.weapon", "무기"),
            ("equipment.ring", "뼈 반지"), ("equipment.tie", "해골 목걸이"), ("equipment.employee_id", "인장"),
        };

        private readonly PlayerProfile _profile;
        private readonly GameDataStore _data;
        private readonly List<SkillData> _skillRows; // Skills의 칸과 같은 순서의 스킬 정의 (능력치 계산용)

        public CharacterInfo Character { get; } = new CharacterInfo
        {
            Name = "캐릭터",
            Rank = "신입 직원",
            Power = 120,
        };

        /// <summary>장비는 Upgrades 테이블과 PlayerProfile에서 만든다. 강화나 레벨 변화가 바로 보이도록 읽을 때마다 새로 만든다</summary>
        public IReadOnlyList<EquipInfo> Equips => CreateEquips();
        public IReadOnlyList<SkillInfo> Skills { get; }

        public int Gems => 550;
        public int Tickets => 0;

        /// <summary>해금 판정은 PlayerProfile의 플레이어 레벨을 따른다. 프로필이 갱신되면(전투 보상 EXP로 레벨이 오른 뒤, 스킬 강화 뒤 등)
        /// 스킬의 해금·레벨·비용을 다시 계산한다</summary>
        public DummyGrowthCatalog(PlayerProfile profile, GameDataStore data)
        {
            _profile = profile;
            _data = data;
            // 습득 카드로 나오는 스킬만 칸을 가진다(다른 스킬이 시전하는 자식 스킬은 제외). 해금 레벨 순서로 놓는다.
            // 스킬 정의는 읽을 때마다 파싱·검증하므로 한 번만 읽어 둔다
            _skillRows = data.LoadSkills()
                .Where(s => !s.childOnly)
                .OrderBy(s => s.unlockLevel)
                .ThenBy(s => s.id)
                .ToList();

            Skills = CreateSkills();
            RefreshSkills();
            _profile.Changed += OnProfileChanged;
        }

        public void Dispose()
        {
            _profile.Changed -= OnProfileChanged;
        }

        private void OnProfileChanged(PlayerProfile _) => RefreshSkills();

        // 스킬 칸의 해금은 플레이어 레벨로 정한다(캐릭터에는 레벨이 없다). 강화 데이터가 있는 스킬은 레벨·비용·능력치도 다시 계산한다.
        // 장비 칸은 Equips를 읽을 때마다 계산한다
        private void RefreshSkills()
        {
            int playerLevel = _profile.Level;
            for (int i = 0; i < Skills.Count; i++)
            {
                var skill = Skills[i];
                skill.IsUnlocked = skill.UnlockLevel <= playerLevel;
                if (skill.UpgradeId != null)
                {
                    ApplyUpgrade(skill, _data.Upgrades.GetOrThrow(skill.UpgradeId), _skillRows[i]);
                }
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

                // 능력치는 강화 팝업의 "현재 / 다음" 두 블록에 그린다: 효과마다 한 줄, 마지막 줄은 장비 레벨. 최대 레벨이면 다음 값이 없다
                var effects = new List<EquipEffect>();
                var stats = new List<StatLine>();
                foreach (var effect in def.Effects)
                {
                    effects.Add(new EquipEffect(EquipEffectText.Format(effect, level, isMax)));
                    stats.Add(new StatLine(EquipEffectText.Name(effect), EquipEffectText.Value(effect, level),
                        isMax ? null : EquipEffectText.Value(effect, next)));
                }

                stats.Add(new StatLine("장비 레벨", level.ToString(), isMax ? null : next.ToString()));

                list.Add(new EquipInfo
                {
                    Slot = i,
                    UpgradeId = upgradeId,
                    Name = name,
                    Level = level,
                    MaxLevel = def.MaxLevel,
                    UnlockLevel = def.UnlockLevel,
                    IsUnlocked = _profile.Level >= def.UnlockLevel,
                    Stats = stats,
                    Effects = effects,
                    CoinCost = isMax ? 0 : def.CostAt(next),
                    MaterialItemId = def.MaterialItemId,
                    MaterialCost = isMax || !def.UsesMaterial ? 0 : def.MaterialAt(next),
                });
            }

            return list;
        }

        // 이름·설명·해금 레벨은 Skills.json에서 온다. progressionId가 Upgrades 테이블에 있으면 강화할 수 있는 스킬이고(UpgradeId),
        // 없으면 기본 능력치만 보여주고 비용·레벨별 보상은 없다
        private List<SkillInfo> CreateSkills()
        {
            var list = new List<SkillInfo>();
            foreach (var data in _skillRows)
            {
                bool upgradable = !string.IsNullOrEmpty(data.progressionId) && _data.Upgrades.Contains(data.progressionId);
                list.Add(new SkillInfo
                {
                    Id = data.id.ToString(),
                    AssetKey = data.assetKey,
                    UpgradeId = upgradable ? data.progressionId : null,
                    Name = data.name,
                    Description = data.desc ?? string.Empty,
                    MaxLevel = NoUpgradeMaxLevel,
                    UnlockLevel = data.unlockLevel,
                    Stats = CreateStats(data, null, 0, true),
                    LevelRewards = Array.Empty<LevelReward>(),
                });
            }

            return list;
        }

        // 강화 데이터가 있는 스킬: 레벨은 프로필, 최대 레벨·다음 비용은 Upgrades 테이블에서 온다. 최대 레벨이면 다음 비용은 0이다
        private void ApplyUpgrade(SkillInfo skill, UpgradeDefinition def, SkillData data)
        {
            int level = _profile.UpgradeLevel(def.UpgradeId);
            bool isMax = level >= def.MaxLevel;
            int next = level + 1;

            if (skill.MaxLevel != def.MaxLevel)
            {
                skill.LevelRewards = CreateRewards(skill.Name, def.MaxLevel);
            }

            skill.Level = level;
            skill.MaxLevel = def.MaxLevel;
            skill.CoinCost = isMax ? 0 : def.CostAt(next);
            skill.MaterialItemId = def.MaterialItemId;
            skill.MaterialCost = isMax || !def.UsesMaterial ? 0 : def.MaterialAt(next);
            skill.Stats = CreateStats(data, def, level, isMax);
        }

        // 능력치는 전투와 같은 계산(Skills.json 기본 스탯 + 영구 강화 효과)으로 현재 값과 다음 레벨 증가량을 만든다.
        // 강화 데이터가 없거나(def == null) 최대 레벨이면 증가량이 없고, 증가량이 없는 줄(예: 피해만 오르는 강화의 쿨타임)은 증가 표시를 하지 않는다
        private static StatLine[] CreateStats(SkillData data, UpgradeDefinition def, int level, bool isMax)
        {
            var current = PermanentSkillEffect.Apply(data, def == null ? null : PermanentSkillEffect.At(def, level)).Stats.Cast;
            var next = def == null || isMax ? current : PermanentSkillEffect.Apply(data, PermanentSkillEffect.At(def, level + 1)).Stats.Cast;
            float damageUp = next.Damage - current.Damage;
            float cooldownDown = next.Cooldown - current.Cooldown;
            return new[]
            {
                new StatLine("공격력", $"{current.Damage:0.#}", increase: damageUp > 0.001f ? $"+{damageUp:0.#}" : null),
                new StatLine("쿨타임", $"{current.Cooldown:0.##}초", increase: cooldownDown < -0.001f ? $"{cooldownDown:0.##}초" : null),
            };
        }

        // 임시: 데이터 시트의 영구 레벨 구간(5/9/13/17/21)은 카드 해금·변형, 나머지는 공격력 증가로 채운다
        // TODO(data): #142 계약이 확정되면 Upgrades 데이터에서 읽는다
        private static List<LevelReward> CreateRewards(string skillName, int maxLevel)
        {
            var list = new List<LevelReward>();
            for (int level = 1; level <= maxLevel; level++)
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
    }
}
