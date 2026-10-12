using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Game.Core
{
    /// <summary>스테이지 테이블 한 행</summary>
    [Serializable]
    public class StageDefinition
    {
        public int Id;
        public int ClearGold;

        /// <summary>엘리트 룰렛의 강화 카드 횟수 범위. 둘 다 0이면 룰렛 보상이 없는 스테이지다</summary>
        public int EliteRouletteMinCards;
        public int EliteRouletteMaxCards;

        /// <summary>rating 1~3 클리어 시 1회성 보상 (index 0 = rating 1)</summary>
        public int[] RatingRewards;

        /// <summary>클리어 시 전투 보상으로 지급할 마법북. 골드는 ClearGold로 관리한다.</summary>
        public List<ItemAmount> ClearItems = new();

        /// <summary>rating 1~3의 1회성 마법북 보상 (index 0 = rating 1). 미설정 등급은 아이템 보상 없음.</summary>
        public List<List<ItemAmount>> RatingItemRewards = new();

        /// <summary>이 스테이지의 웨이브에 한 번이라도 나오는 몬스터 ID. 웨이브 구성에서 첫 등장 순서로 모은다.
        /// 데이터에 따로 적지 않는다. 일시정지 창의 "등장 요마"가 이 순서대로 보여준다</summary>
        [JsonIgnore]
        public IReadOnlyList<int> MonsterIds
        {
            get
            {
                var ids = new List<int>();
                if (Waves == null) { return ids; }
                foreach (var wave in Waves)
                {
                    if (wave?.Spawns == null) { continue; }
                    foreach (var spawn in wave.Spawns)
                    {
                        if (spawn != null && !ids.Contains(spawn.MonsterId)) { ids.Add(spawn.MonsterId); }
                    }
                }

                return ids;
            }
        }

        /// <summary>이 스테이지에서 나오는 모든 몬스터의 능력치가 늘어나는 비율. 체력·공격력·이동 속도를 함께 올릴 수 있고, 비어 있으면 그대로다.
        /// 미션 팝업이 같은 목록을 안내한다</summary>
        public List<MonsterBonus> MonsterBonuses = new();

        public int WallHp;
        public SpawnDefinition Spawn;

        /// <summary>진행 순서대로의 웨이브. 마지막 웨이브가 최종 웨이브다</summary>
        public List<WaveDefinition> Waves;

        /// <summary>능력치의 증가율(%). 없으면 0</summary>
        public int BonusPercent(MonsterStat stat)
        {
            foreach (var bonus in MonsterBonuses ?? new List<MonsterBonus>())
            {
                if (bonus.GetStat() == stat) { return bonus.Percent; }
            }

            return 0;
        }

        /// <summary>몬스터 기본 체력에 이 스테이지의 증가율을 적용한 체력. 올림해서 증가율이 있으면 최소 1은 늘어난다</summary>
        public int ScaleMonsterHp(int baseHp) => ScaleUp(baseHp, BonusPercent(MonsterStat.Hp));

        public int ScaleMonsterDamage(int baseDamage) => ScaleUp(baseDamage, BonusPercent(MonsterStat.Damage));

        public float ScaleMonsterSpeed(float baseSpeed) => baseSpeed * (100 + BonusPercent(MonsterStat.Speed)) / 100f;

        private static int ScaleUp(int value, int percent) => (int)(((long)value * (100 + percent) + 99) / 100);

        public void Validate()
        {
            if (EliteRouletteMinCards < 0 || EliteRouletteMaxCards < EliteRouletteMinCards
                || EliteRouletteMaxCards > EliteRouletteSession.MaxCards
                || (EliteRouletteMinCards == 0 && EliteRouletteMaxCards != 0))
            {
                throw new InvalidOperationException($"Stages {Id}: 엘리트 룰렛 횟수는 1~{EliteRouletteSession.MaxCards} 범위이거나 둘 다 0이어야 합니다");
            }

            var bonusStats = new HashSet<MonsterStat>();
            foreach (var bonus in MonsterBonuses ?? new List<MonsterBonus>())
            {
                if (bonus == null || bonus.Percent <= 0 || !bonusStats.Add(bonus.GetStat()))
                {
                    throw new InvalidOperationException($"Stages {Id}: 몬스터 강화는 능력치마다 한 줄이고 증가율은 1 이상이어야 합니다");
                }
            }

            ValidateItemRewards(ClearItems);
            if (RatingItemRewards != null)
            {
                if (RatingItemRewards.Count > 3)
                {
                    throw new InvalidOperationException($"Stages {Id}: 아이템 등급 보상은 최대 3단계입니다");
                }
                foreach (var rewards in RatingItemRewards) { ValidateItemRewards(rewards); }
            }

            if (WallHp <= 0)
            {
                throw new InvalidOperationException($"Stages {Id}: WallHp는 1 이상이어야 합니다");
            }

            if (Spawn == null || Spawn.IntervalMin <= 0f || Spawn.IntervalMax < Spawn.IntervalMin || Spawn.Cooldown < 0f)
            {
                throw new InvalidOperationException($"Stages {Id}: Spawn 간격이 잘못되었습니다 (0 < IntervalMin <= IntervalMax, Cooldown >= 0)");
            }

            if (Waves == null || Waves.Count != StageRewardRules.WaveCount)
            {
                throw new InvalidOperationException($"Stages {Id}: 모든 스테이지는 {StageRewardRules.WaveCount}웨이브입니다");
            }

            for (int i = 0; i < Waves.Count; i++)
            {
                var wave = Waves[i];
                if (wave?.Spawns == null || wave.Spawns.Count == 0)
                {
                    throw new InvalidOperationException($"Stages {Id}: {i + 1}웨이브에 몬스터 구성(Spawns)이 필요합니다");
                }

                var seen = new HashSet<int>();
                foreach (var spawn in wave.Spawns)
                {
                    if (spawn == null || spawn.Count < 1 || !seen.Add(spawn.MonsterId))
                    {
                        throw new InvalidOperationException($"Stages {Id}: {i + 1}웨이브의 구성은 중복 없는 몬스터와 1 이상의 마릿수여야 합니다");
                    }
                }
            }
        }

        private void ValidateItemRewards(IEnumerable<ItemAmount> rewards)
        {
            if (rewards == null) { return; }
            foreach (var item in rewards)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.itemId) || item.quantity <= 0)
                {
                    throw new InvalidOperationException($"Stages {Id}: 아이템 보상은 ID와 양수 수량이 필요합니다");
                }
            }
        }
    }
}
