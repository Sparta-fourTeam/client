using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    public sealed class StageRewards
    {
        public int Coin { get; internal set; }
        public int Exp { get; internal set; }
        public List<ItemAmount> Items { get; } = new();
    }

    /// <summary>지급 시점은 공통 규칙이며 수량·재료 선택은 스테이지 밸런스다.</summary>
    public static class StageRewardRules
    {
        public const int WaveCount = 20;
        public const int StartCoin = 25, CoinPerWave = 5;
        public const int StartSkillMaterial = 1, SkillMaterialPerWave = 1;

        public static StageRewards Calculate(StageRewardBalance balance, int completedWaves, bool cleared, int rating)
        {
            balance.Validate();
            if (completedWaves < 0 || completedWaves > WaveCount || (cleared && completedWaves != WaveCount)
                || rating < 0 || rating > 3 || (!cleared && rating != 0) || (cleared && rating == 0))
            { throw new ArgumentException("완료 웨이브와 클리어·별 등급이 일치해야 합니다."); }
            var result = new StageRewards
            {
                Coin = checked(StartCoin + CoinPerWave * completedWaves + (completedWaves >= 6 ? balance.Wave6CoinBonus : 0)),
                Exp = checked((completedWaves >= 1 ? balance.BaseExp : 0) + (cleared && rating == 3 ? balance.ThreeStarBonusExp : 0))
            };
            result.Items.Add(new ItemAmount
            {
                itemId = balance.SkillMaterial,
                quantity = checked(StartSkillMaterial + SkillMaterialPerWave * completedWaves
                    + (completedWaves >= 3 ? balance.Wave3SkillMaterialBonusAmount : 0))
            });
            if (completedWaves >= 4)
            {
                result.Items.AddRange(balance.Wave4EquipmentMaterials.Where(item => item.quantity > 0)
                    .Select(item => new ItemAmount { itemId = item.itemId, quantity = item.quantity }));
            }
            if (completedWaves >= 5 && balance.Wave5GemChestCount > 0)
            { result.Items.Add(new ItemAmount { itemId = ItemIds.GemChest, quantity = balance.Wave5GemChestCount }); }
            return result;
        }

        public static StageRewards Maximum(StageRewardBalance balance) => Calculate(balance, WaveCount, true, 3);
        public static int ClearRating(int wallHpPercent) => wallHpPercent >= 100 ? 3 : wallHpPercent >= 50 ? 2 : 1;
    }
}
