using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Game.Core
{
    public sealed class StageRewards
    {
        public int Coin { get; internal set; }
        public int Exp { get; internal set; }
        public int BonusExp { get; internal set; }
        public List<ItemAmount> Items { get; } = new();
    }

    /// <summary>한 보상의 공통 지급 규칙: 시작 수량, 웨이브 하나를 끝낼 때마다 느는 수량, 중간 보너스를 받는 웨이브.
    /// 보너스 수량은 스테이지가 정한 총수량에서 계산한다</summary>
    public readonly struct RewardSchedule
    {
        [JsonProperty(Required = Required.Always)] public int Start { get; }
        [JsonProperty(Required = Required.Always)] public int PerWave { get; }
        [JsonProperty(Required = Required.Always)] public int BonusWave { get; }

        [JsonConstructor]
        public RewardSchedule(int start, int perWave, int bonusWave)
        {
            Start = start;
            PerWave = perWave;
            BonusWave = bonusWave;
        }

        /// <summary>기본 누적만으로 전체 웨이브를 끝냈을 때의 수량. 총수량은 이 값 이상이어야 한다</summary>
        public int MinimumFinal => checked(Start + PerWave * StageRewardRules.WaveCount);

        public int Amount(int completedWaves, int finalAmount)
        {
            int bonus = completedWaves >= BonusWave ? finalAmount - MinimumFinal : 0;
            return checked(Start + PerWave * completedWaves + bonus);
        }
    }

    /// <summary>지급 시점은 공통 규칙(RewardRules)이며 총수량·재료 선택은 스테이지 밸런스다.</summary>
    public static class StageRewardRules
    {
        public const int WaveCount = 20;

        public static StageRewards Calculate(StageRewardBalance balance, int completedWaves, bool cleared, int rating)
        {
            balance.Validate();
            if (completedWaves < 0 || completedWaves > WaveCount || (cleared && completedWaves != WaveCount)
                || rating < 0 || rating > 3 || (!cleared && rating != 0) || (cleared && rating == 0))
            { throw new ArgumentException("완료 웨이브와 클리어·별 등급이 일치해야 합니다."); }
            int bonusExp = cleared && rating == 3 ? balance.ThreeStarBonusExp : 0;
            var result = new StageRewards
            {
                Coin = balance.RewardRules.Coin.Amount(completedWaves, balance.FinalCoin),
                Exp = checked((completedWaves >= 1 ? balance.BaseExp : 0) + bonusExp),
                BonusExp = bonusExp
            };
            foreach (var item in balance.Items)
            {
                balance.RewardRules.TryGetItem(item.itemId, out var schedule);
                int quantity = schedule.Amount(completedWaves, item.finalAmount);
                if (quantity > 0) { result.Items.Add(new ItemAmount { itemId = item.itemId, quantity = quantity }); }
            }
            return result;
        }

        public static StageRewards Maximum(StageRewardBalance balance) => Calculate(balance, WaveCount, true, 3);
        public static int ClearRating(int wallHpPercent) => wallHpPercent >= 100 ? 3 : wallHpPercent >= 50 ? 2 : 1;
    }
}
