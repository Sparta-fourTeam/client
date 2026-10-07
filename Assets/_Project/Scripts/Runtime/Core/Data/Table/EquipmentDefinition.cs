using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>장비 효과 하나. 레벨마다 ValuePerLevel만큼 늘어난다 (장비 레벨 L의 효과 = ValuePerLevel × L)</summary>
    [Serializable]
    public class EquipmentEffectDefinition
    {
        public string Kind;
        public float ValuePerLevel;
    }

    /// <summary>장비 테이블 한 행. 장비에는 장착·해제가 없고 강화만 있다. 해금은 플레이어 레벨로 정한다</summary>
    [Serializable]
    public class EquipmentDefinition
    {
        public string Id;

        /// <summary>이 장비를 강화할 때 소모하는 재료 아이템 ID (Items 테이블에서 TargetId가 이 장비 ID인 아이템)</summary>
        public string MaterialItemId;

        /// <summary>이 플레이어 레벨부터 강화할 수 있다</summary>
        public int UnlockLevel;

        public int MaxLevel;

        /// <summary>level(1부터)에 도달하기 위한 코인·재료 비용. 첫 강화(레벨 0 → 1)는 index 0이다</summary>
        public int[] CoinCosts;
        public int[] MaterialCosts;

        public List<EquipmentEffectDefinition> Effects = new();

        public int CoinAt(int level) => CoinCosts[level - 1];
        public int MaterialAt(int level) => MaterialCosts[level - 1];

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(MaterialItemId))
            {
                throw new InvalidOperationException("Equipment: Id와 MaterialItemId가 필요합니다");
            }

            if (UnlockLevel < 1 || MaxLevel < 1)
            {
                throw new InvalidOperationException($"Equipment {Id}: UnlockLevel과 MaxLevel은 1 이상이어야 합니다");
            }

            if (CoinCosts == null || MaterialCosts == null || CoinCosts.Length != MaxLevel || MaterialCosts.Length != MaxLevel)
            {
                throw new InvalidOperationException($"Equipment {Id}: CoinCosts와 MaterialCosts는 MaxLevel({MaxLevel})개여야 합니다");
            }

            foreach (int cost in CoinCosts)
            {
                if (cost <= 0) { throw new InvalidOperationException($"Equipment {Id}: 코인 비용은 양수여야 합니다"); }
            }

            foreach (int cost in MaterialCosts)
            {
                if (cost <= 0) { throw new InvalidOperationException($"Equipment {Id}: 재료 비용은 양수여야 합니다"); }
            }

            foreach (var effect in Effects)
            {
                if (effect == null || string.IsNullOrWhiteSpace(effect.Kind))
                {
                    throw new InvalidOperationException($"Equipment {Id}: 효과에는 Kind가 필요합니다");
                }
            }
        }
    }
}
