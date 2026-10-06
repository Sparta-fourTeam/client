using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>IGrowthCatalog의 하드코딩 구현. 화면 확인용 임시 값이다.
    /// TODO(data): 스킬은 Upgrades.json + PlayerProfile.UpgradeLevel, 캐릭터·장비는 데이터와 API가 생기면 실제 구현으로 교체</summary>
    public sealed class DummyGrowthCatalog : IGrowthCatalog
    {
        private const int PlayerLevel = 3;
        private const int SkillMaxLevel = 30;

        public CharacterInfo Character { get; } = new CharacterInfo
        {
            Name = "캐릭터",
            Rank = "신입 직원",
            Level = 3,
            Power = 120,
        };

        public IReadOnlyList<EquipInfo> Equips { get; }
        public IReadOnlyList<SkillInfo> Skills { get; }

        public int SkillBooks => 2;
        public int EquipBooks => 4;
        public int Gems => 550;
        public int Tickets => 0;

        public DummyGrowthCatalog()
        {
            Equips = CreateEquips();
            Skills = CreateSkills();
        }

        // 원작 장비 6종 + 왼쪽 칸 1개. 칸은 캐릭터 레벨 2~8에서 하나씩 열린다
        private List<EquipInfo> CreateEquips()
        {
            string[] names = { "모자", "상의", "신발", "단검", "반지", "넥타이", "사원증" };
            int[] levels = { 2, 8, 0, 0, 0, 0, 1 };
            var list = new List<EquipInfo>();
            for (int i = 0; i < names.Length; i++)
            {
                int unlockLevel = i + 2;
                bool unlocked = unlockLevel <= Character.Level || i == names.Length - 1;
                list.Add(new EquipInfo
                {
                    Slot = i,
                    Name = names[i],
                    Level = levels[i],
                    UnlockLevel = unlockLevel,
                    IsUnlocked = unlocked,
                    Stats = new[]
                    {
                        new StatLine("공격력", (levels[i] * 2).ToString()),
                        new StatLine("장비 레벨", levels[i].ToString()),
                    },
                    Effects = i == 1
                        ? new[]
                        {
                            new EquipEffect("[기술력] 단검 대미지 18% ▲"),
                            new EquipEffect("[화력] 화염탄 대미지 10% ▲"),
                            new EquipEffect("[집중력] 방벽 내구도 30% 미만일 때 대미지 60% ▲"),
                            new EquipEffect("[전력] 벼락 대미지 9% ▲", isActive: false),
                            new EquipEffect("[기술력] 단검 투척 대미지 6% ▲, 관통력 +1"),
                        }
                        : new[] { new EquipEffect("[기술력] 단검 대미지 5% ▲") },
                    CoinCost = 100 + levels[i] * 15,
                    BookCost = 1 + levels[i],
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
                list.Add(new SkillInfo
                {
                    Id = n.id,
                    Name = n.name,
                    Description = n.desc,
                    Level = n.level,
                    MaxLevel = SkillMaxLevel,
                    UnlockLevel = 1,
                    IsUnlocked = true,
                    Stats = new[]
                    {
                        new StatLine("공격력", Attack(n.attack, n.level).ToString(), Attack(n.attack, n.level + 1).ToString()),
                        new StatLine("쿨타임", $"{Cooldown(n.cooldown, n.level):0.##}초", $"{Cooldown(n.cooldown, n.level + 1):0.##}초"),
                        new StatLine("기술 레벨", n.level.ToString(), (n.level + 1).ToString()),
                    },
                    CoinCost = 100 + n.level * 50,
                    BookCost = 1 + n.level / 3,
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
                    IsUnlocked = unlockLevel <= PlayerLevel,
                    Stats = new StatLine[0],
                });
            }
            return list;
        }

        // 임시 공식: 공격력은 레벨당 기본값의 20%씩, 쿨타임은 3레벨마다 5%씩 줄어든다
        private static int Attack(int baseAttack, int level) => baseAttack + baseAttack * level / 5;
        private static float Cooldown(float baseCooldown, int level) => baseCooldown * (1f - 0.05f * level / 3f);
    }
}
