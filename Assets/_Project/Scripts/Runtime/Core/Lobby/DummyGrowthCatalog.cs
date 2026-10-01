using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>IGrowthCatalog의 하드코딩 구현. 화면 확인용 임시 값이다.
    /// TODO(data): 인술은 Upgrades.json + PlayerProfile.UpgradeLevel, 캐릭터·장비는 데이터와 API가 생기면 실제 구현으로 교체</summary>
    public sealed class DummyGrowthCatalog : IGrowthCatalog
    {
        private const int PlayerLevel = 3;
        private const int NinpoMaxLevel = 30;

        public ShinobiInfo Shinobi { get; } = new ShinobiInfo
        {
            Name = "료 나카츠카미",
            Rank = "하급 닌자",
            Level = 3,
            Power = 120,
        };

        public IReadOnlyList<EquipInfo> Equips { get; }
        public IReadOnlyList<NinpoInfo> Ninpos { get; }

        public int NinpoBooks => 2;
        public int EquipBooks => 4;
        public int Gems => 550;
        public int Tickets => 0;

        public DummyGrowthCatalog()
        {
            Equips = CreateEquips();
            Ninpos = CreateNinpos();
        }

        // 원작 장비 6종 + 왼쪽 칸 1개. 칸은 캐릭터 레벨 2~8에서 하나씩 열린다
        private List<EquipInfo> CreateEquips()
        {
            string[] names = { "두건", "상의", "신발", "쿠나이", "반지", "스카프", "부적" };
            int[] levels = { 2, 8, 0, 0, 0, 0, 1 };
            var list = new List<EquipInfo>();
            for (int i = 0; i < names.Length; i++)
            {
                int unlockLevel = i + 2;
                bool unlocked = unlockLevel <= Shinobi.Level || i == names.Length - 1;
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
                            new EquipEffect("[비둔주] 쿠나이 대미지 18% ▲"),
                            new EquipEffect("[화둔주] 화둔술 대미지 10% ▲"),
                            new EquipEffect("[인법주] 방벽 내구도 30% 미만일 때 대미지 60% ▲"),
                            new EquipEffect("[뇌둔주] 벼락 대미지 9% ▲", isActive: false),
                            new EquipEffect("[비둔주] 쿠나이 투척 대미지 6% ▲, 관통력 +1"),
                        }
                        : new[] { new EquipEffect("[비둔주] 쿠나이 대미지 5% ▲") },
                    CoinCost = 100 + levels[i] * 15,
                    BookCost = 1 + levels[i],
                });
            }
            return list;
        }

        // 6개는 열려 있고, 나머지는 플레이어 레벨 4부터 하나씩 열린다
        private List<NinpoInfo> CreateNinpos()
        {
            (string id, string name, string desc, int level, int attack, float cooldown)[] open =
            {
                ("shuriken", "수리검", "가장 가까운 적에게 수리검을 던진다", 3, 10, 1.0f),
                ("fireball", "화염탄", "적에게 닿으면 폭발하는 화염탄을 쏜다", 0, 15, 2.0f),
                ("frost", "빙결", "주변 적을 얼려 느리게 만든다", 0, 8, 3.0f),
                ("lightning", "낙뢰", "무작위 적에게 벼락을 떨어뜨린다", 0, 20, 2.5f),
                ("thorn", "가시", "바닥에 가시를 깔아 지나가는 적을 찌른다", 0, 6, 4.0f),
                ("flash", "섬광", "앞쪽 적을 꿰뚫는 빛줄기를 쏜다", 0, 12, 3.0f),
            };

            var list = new List<NinpoInfo>();
            foreach (var n in open)
            {
                list.Add(new NinpoInfo
                {
                    Id = n.id,
                    Name = n.name,
                    Description = n.desc,
                    Level = n.level,
                    MaxLevel = NinpoMaxLevel,
                    UnlockLevel = 1,
                    IsUnlocked = true,
                    Stats = new[]
                    {
                        new StatLine("공격력", Attack(n.attack, n.level).ToString(), Attack(n.attack, n.level + 1).ToString()),
                        new StatLine("쿨타임", $"{Cooldown(n.cooldown, n.level):0.##}초", $"{Cooldown(n.cooldown, n.level + 1):0.##}초"),
                        new StatLine("인술 레벨", n.level.ToString(), (n.level + 1).ToString()),
                    },
                    CoinCost = 100 + n.level * 50,
                    BookCost = 1 + n.level / 3,
                });
            }

            for (int unlockLevel = 4; unlockLevel <= 15; unlockLevel++)
            {
                list.Add(new NinpoInfo
                {
                    Id = "locked" + unlockLevel,
                    Name = "???",
                    Description = string.Empty,
                    MaxLevel = NinpoMaxLevel,
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
