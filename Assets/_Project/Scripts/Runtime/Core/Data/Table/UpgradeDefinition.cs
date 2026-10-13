using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>강화 효과 하나. 레벨마다 ValuePerLevel만큼 늘어난다 (레벨 L의 효과 = ValuePerLevel × L)</summary>
    [Serializable]
    public class UpgradeStatEffect
    {
        public string Kind;
        public float ValuePerLevel;
    }

    /// <summary>업그레이드 테이블 한 행. 장비 강화와 스킬 강화가 같은 구조를 쓴다 (장비에는 장착·해제가 없고 강화만 있다)</summary>
    [Serializable]
    public class UpgradeDefinition
    {
        public string UpgradeId;
        public int MaxLevel;

        /// <summary>level(1부터)에 도달하기 위한 코인 비용. 첫 구매(레벨 0 → 1)는 index 0이다</summary>
        public int[] LevelCosts;

        /// <summary>이 플레이어 레벨부터 강화할 수 있다. 값을 쓰지 않으면 처음부터 가능하다</summary>
        public int UnlockLevel = 1;

        /// <summary>강화할 때 코인과 함께 소모하는 재료 아이템 ID (Items 테이블). 비어 있으면 코인만 든다</summary>
        public string MaterialItemId;

        /// <summary>level(1부터)에 도달하기 위한 재료 수량. 재료를 쓰는 강화만 MaxLevel개를 채운다</summary>
        public int[] MaterialCosts;

        public List<UpgradeStatEffect> Effects = new();

        public bool UsesMaterial => !string.IsNullOrWhiteSpace(MaterialItemId);

        /// <summary>level(1부터 시작)에 도달하기 위한 비용. 아직 구매하지 않은 상태(레벨 0)에서 첫 구매는 CostAt(1)이다</summary>
        public int CostAt(int level) => LevelCosts[level - 1];

        public int MaterialAt(int level) => MaterialCosts[level - 1];

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(UpgradeId))
            {
                throw new InvalidOperationException("Upgrades: UpgradeId가 필요합니다");
            }

            if (UnlockLevel < 1 || MaxLevel < 1)
            {
                throw new InvalidOperationException($"Upgrades {UpgradeId}: UnlockLevel과 MaxLevel은 1 이상이어야 합니다");
            }

            if (LevelCosts == null || LevelCosts.Length != MaxLevel)
            {
                throw new InvalidOperationException($"Upgrades {UpgradeId}: LevelCosts는 MaxLevel({MaxLevel})개여야 합니다");
            }

            foreach (int cost in LevelCosts)
            {
                if (cost <= 0) { throw new InvalidOperationException($"Upgrades {UpgradeId}: 코인 비용은 양수여야 합니다"); }
            }

            if (UsesMaterial)
            {
                if (MaterialCosts == null || MaterialCosts.Length != MaxLevel)
                {
                    throw new InvalidOperationException($"Upgrades {UpgradeId}: MaterialCosts는 MaxLevel({MaxLevel})개여야 합니다");
                }

                foreach (int cost in MaterialCosts)
                {
                    if (cost <= 0) { throw new InvalidOperationException($"Upgrades {UpgradeId}: 재료 비용은 양수여야 합니다"); }
                }
            }
            else if (MaterialCosts != null && MaterialCosts.Length > 0)
            {
                throw new InvalidOperationException($"Upgrades {UpgradeId}: MaterialItemId 없이 MaterialCosts를 쓸 수 없습니다");
            }

            foreach (var effect in Effects)
            {
                if (effect == null || string.IsNullOrWhiteSpace(effect.Kind))
                {
                    throw new InvalidOperationException($"Upgrades {UpgradeId}: 효과에는 Kind가 필요합니다");
                }
            }
        }
    }
}
