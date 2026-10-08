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

        public int WallHp;
        public SpawnDefinition Spawn;

        /// <summary>진행 순서대로의 웨이브. 마지막 웨이브가 최종 웨이브다</summary>
        public List<WaveDefinition> Waves;

        public void Validate()
        {
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
